package it.moscorun.app.science

import kotlinx.coroutines.CancellationException
import java.time.Duration
import java.time.Instant

/** How far the scientific part has been confirmed by the server, per account and source. */
interface ScienceStateStore {
    fun scienceSyncedUntil(scope: String, source: String): Instant?
    fun saveScienceSyncedUntil(scope: String, source: String, until: Instant)
}

/** Bounds of one scientific run. */
data class ScienceLimits(
    /** First run without the permission to older data: Health Connect shows only the last 30 days. */
    val initialDays: Int = 30,
    /** First run with the permission to older data. */
    val historyDays: Int = 365,
    /** Days read again at every run, for values the source corrects late (sleep edited in the morning, HRV recomputed). */
    val revisitDays: Int = 7,
    val windowDays: Int = 7,
    /** Kept away from now, so a phone clock slightly ahead never sends a time the server sees in the future. */
    val settle: Duration = Duration.ofMinutes(5),
)

enum class ScienceState {
    COMPLETED,

    /** Stopped by the time budget: it continues at the next run. */
    MORE_WORK,

    /** Nothing to read now: no permission, Health Connect missing, or an old server. Workouts are not affected. */
    SKIPPED,
    RETRY_LATER,
    AUTH_REQUIRED,
}

data class ScienceResult(val state: ScienceState, val sent: Int = 0, val imported: Int = 0, val rejected: Int = 0, val message: String, val diagnostic: ScienceDiagnosticReport? = null) {
    fun describe(): String = buildString {
        append(message)
        if (sent > 0 || rejected > 0) append(" (osservazioni: $imported nuove su $sent inviate")
        if (rejected > 0) append(", $rejected rifiutate")
        if (sent > 0 || rejected > 0) append(")")
        diagnostic?.let { append(". Dettaglio scientifico: ").append(it.describe()) }
    }
}

/**
 * Copies daily scientific observations to the server, oldest window first, after the workouts.
 *
 * Durability: the position advances only after the server confirmed a whole window; everything sent twice is
 * recognised by the server as the same revision. A batch the server refuses is split in halves until the
 * refused observations are isolated, so one bad value never holds back the others; refused values are counted,
 * not retried, because sending the same content again cannot succeed.
 */
class ScienceSync(
    private val reader: ScienceReader,
    private val server: ScienceServer,
    private val store: ScienceStateStore,
    private val scope: String,
    private val limits: ScienceLimits = ScienceLimits(),
    private val now: () -> Instant = Instant::now,
    private val shouldStop: () -> Boolean = { false },
) {
    private val diagnostics = ScienceDiagnostics(reader.source, now(), reader.supportedMetrics)
    private var sent = 0
    private var imported = 0
    private var rejected = 0
    private var lastRejection: String? = null

    private class Halt(val state: ScienceState, val reason: String) : Exception(reason)

    suspend fun run(): ScienceResult = try {
        execute()
    } catch (e: CancellationException) {
        throw e
    } catch (e: Halt) {
        result(e.state, e.reason)
    } catch (_: SecurityException) {
        result(ScienceState.SKIPPED, "La sorgente non consente la lettura dei dati scientifici ora: apri MoscoRun e controlla i permessi.")
    } catch (e: Exception) {
        result(ScienceState.SKIPPED, "Lettura dei dati scientifici non riuscita (${e.javaClass.simpleName}): nuovo tentativo alla prossima sincronizzazione.")
    }

    private suspend fun execute(): ScienceResult {
        val availability = reader.availability()
        diagnostics.permissions(availability)
        if (availability.metrics.isEmpty()) return result(ScienceState.SKIPPED, availability.message ?: "Nessun dato scientifico da leggere.")

        val end = now().minus(limits.settle)
        if (reader.isolatedStreams) return executeStreams(availability, end)
        val checkpoint = reader.checkpoint(availability)
        val confirmed = store.scienceSyncedUntil(scope, checkpoint)
        var from = confirmed?.minus(days(limits.revisitDays))
            ?: end.minus(days(if (availability.history) limits.historyDays else limits.initialDays))
        while (from.isBefore(end)) {
            if (shouldStop()) return result(ScienceState.MORE_WORK, "Dati scientifici in corso, riprendono alla prossima sincronizzazione")
            val to = minOf(end, from.plus(days(limits.windowDays)))
            val observations = reader.read(from, to, availability.metrics)
            diagnostics.read(from, to, observations, availability.metrics)
            observations.chunked(ScienceUpload.MAX_BATCH).forEach {
                diagnostics.attempted(it)
                send(it)
            }
            // Never moves backwards: a revisit of days already confirmed leaves the position where it was.
            if (confirmed == null || to.isAfter(confirmed)) store.saveScienceSyncedUntil(scope, checkpoint, to)
            from = to
        }
        val message = lastRejection?.let { "Dati scientifici aggiornati; ultimo rifiuto del server: $it" }
            ?: availability.message?.let { "Dati scientifici aggiornati. $it" }
            ?: "Dati scientifici aggiornati"
        return result(ScienceState.COMPLETED, message)
    }

    /**
     * Independent two-sided coverage per permission group. Start with recent measures, then fill older windows
     * round-robin. A failed SDK read leaves that stream's coverage untouched and cannot block other metrics.
     */
    private suspend fun executeStreams(availability: ScienceAvailability, end: Instant): ScienceResult {
        val floor = end.minus(days(if (availability.history) limits.historyDays else limits.initialDays))
        val streams = availability.metrics.intersect(reader.supportedMetrics).map { group ->
            val key = reader.checkpoint(availability.copy(metrics = setOf(group))) + ":coverage-v2"
            val width = if (group in setOf(ScienceMetric.HEART_RATE, ScienceMetric.SAMSUNG_HEART,
                    ScienceMetric.SAMSUNG_OXYGEN, ScienceMetric.SAMSUNG_SKIN)) Duration.ofDays(1)
                else days(limits.windowDays)
            val newer = store.scienceSyncedUntil(scope, "$key:newer")
            val older = store.scienceSyncedUntil(scope, "$key:older")
            Stream(group, key, width, newer, older)
        }.sortedBy { it.newer ?: Instant.MIN }
        val failed = mutableSetOf<ScienceMetric>()

        suspend fun readWindow(stream: Stream, from: Instant, to: Instant): Boolean {
            if (shouldStop()) throw Halt(ScienceState.MORE_WORK, "Dati scientifici in corso; checkpoint conservati.")
            val observations = try {
                reader.read(from, to, setOf(stream.group))
            } catch (e: CancellationException) {
                throw e
            } catch (_: Exception) {
                failed += stream.group
                diagnostics.readFailed(setOf(stream.group))
                return false
            }
            diagnostics.read(from, to, observations, setOf(stream.group))
            observations.chunked(ScienceUpload.MAX_BATCH).forEach {
                if (shouldStop()) throw Halt(ScienceState.MORE_WORK, "Invio scientifico interrotto; finestra da ripetere.")
                diagnostics.attempted(it)
                send(it)
            }
            return true
        }

        // Give each stream a turn before importing the next window of any stream.
        var advanced: Boolean
        do {
            advanced = false
            for (stream in streams) {
                if (stream.group in failed || (stream.newer != null && !stream.newer!!.isBefore(end))) continue
                val previous = stream.newer
                val overlap = minOf(days(limits.revisitDays), stream.width.dividedBy(4))
                val from = if (previous == null) maxOf(floor, end.minus(stream.width))
                    else maxOf(stream.older ?: floor, floor, previous.minus(overlap))
                val to = minOf(end, from.plus(stream.width))
                if (!from.isBefore(to)) continue
                if (!readWindow(stream, from, to)) continue
                if (previous == null || stream.older == null) {
                    stream.older = from
                    store.saveScienceSyncedUntil(scope, "${stream.key}:older", from)
                }
                stream.newer = to
                store.saveScienceSyncedUntil(scope, "${stream.key}:newer", to)
                advanced = true
            }
        } while (advanced)

        do {
            advanced = false
            for (stream in streams) {
                if (stream.group in failed) continue
                val to = stream.older ?: continue
                if (!to.isAfter(floor)) continue
                val from = maxOf(floor, to.minus(stream.width))
                if (!readWindow(stream, from, to)) continue
                stream.older = from
                store.saveScienceSyncedUntil(scope, "${stream.key}:older", from)
                advanced = true
            }
        } while (advanced)

        if (failed.isNotEmpty()) return result(ScienceState.RETRY_LATER,
            "Lettura incompleta: " + failed.joinToString(", ") { it.label } + ". Gli altri flussi restano sincronizzati.")
        return result(ScienceState.COMPLETED, lastRejection?.let { "Dati aggiornati; ultimo rifiuto: $it" }
            ?: "Dati scientifici aggiornati")
    }

    private data class Stream(val group: ScienceMetric, val key: String, val width: Duration,
        var newer: Instant?, var older: Instant?)

    private fun send(batch: List<ScienceObservation>) {
        val answer = server.postObservations(ScienceUpload.json(reader.source, batch))
        when (answer.outcome) {
            ScienceOutcome.STORED -> {
                sent += batch.size
                imported += answer.imported
                diagnostics.confirmed(batch)
            }
            ScienceOutcome.INVALID, ScienceOutcome.CONFLICT ->
                if (batch.size > 1) {
                    send(batch.subList(0, batch.size / 2))
                    send(batch.subList(batch.size / 2, batch.size))
                } else {
                    rejected++
                    diagnostics.rejected(batch.single(), answer.outcome == ScienceOutcome.CONFLICT)
                    lastRejection = answer.detail?.take(MAX_DETAIL)
                }
            ScienceOutcome.UNAUTHORIZED -> throw Halt(ScienceState.AUTH_REQUIRED, answer.detail ?: "Token non accettato dal server.")
            ScienceOutcome.UNSUPPORTED -> throw Halt(ScienceState.SKIPPED, "Il server non riceve ancora i dati scientifici: aggiornarlo.")
            ScienceOutcome.RETRY -> throw Halt(ScienceState.RETRY_LATER, "Dati scientifici rimandati: ${answer.detail ?: "server non raggiungibile"}")
        }
    }

    private fun result(state: ScienceState, message: String) = ScienceResult(state, sent, imported, rejected, message, diagnostics.report(state, now()))

    private fun days(count: Int) = Duration.ofDays(count.toLong())

    private companion object {
        const val MAX_DETAIL = 200
    }
}
