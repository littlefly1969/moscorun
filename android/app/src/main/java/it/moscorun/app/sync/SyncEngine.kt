package it.moscorun.app.sync

import it.moscorun.app.model.SourceWorkout
import it.moscorun.app.server.ReadResult
import it.moscorun.app.server.RemoteWorkout
import it.moscorun.app.server.SyncServer
import it.moscorun.app.server.UploadMapper
import it.moscorun.app.server.UploadOutcome
import it.moscorun.app.server.WorkoutFormat
import it.moscorun.app.source.SourceBlockedException
import it.moscorun.app.source.SourceStatus
import it.moscorun.app.source.WorkoutSource
import java.time.Duration
import java.time.Instant
import kotlin.coroutines.cancellation.CancellationException

/** What a run is doing, for the home screen. */
data class SyncProgress(val activity: String, val report: SyncReport)

/**
 * Copies workouts from a source to the server in blocks of time, newest first.
 *
 * Durability: a workout is marked as synchronised only after the server confirmed it, and a block is marked as
 * covered only after every workout in it was handled. An interruption anywhere (process death, lost network,
 * the system stopping background work) leaves a state the next run continues from; anything sent twice is
 * recognised by the server as the same workout.
 *
 * Memory: one page of workouts, and one serialised workout, at a time.
 */
class SyncEngine(
    private val source: WorkoutSource,
    private val server: SyncServer,
    private val store: SyncStateStore,
    private val scope: String,
    private val plan: SyncPlan,
    private val limits: SyncLimits = SyncLimits(),
    private val now: () -> Instant = Instant::now,
    private val shouldStop: () -> Boolean = { false },
    private val progress: (SyncProgress) -> Unit = {},
) {
    private val provider = source.provider
    private var report = SyncReport()

    private class Block(val start: Instant, val end: Instant, val resumeAt: Instant?, val backward: Boolean)

    private sealed interface Step {
        /** Keep going with the next workout or block. */
        data object Continue : Step

        /** The run cannot continue. */
        data class Halt(val outcome: RunOutcome, val message: String) : Step
    }

    suspend fun run(): SyncResult {
        val started = now()
        val previous = store.status(scope)
        store.saveStatus(scope, previous.copy(lastAttemptAt = started))
        val result = try {
            execute(started)
        } catch (e: CancellationException) {
            throw e
        } catch (e: SourceBlockedException) {
            SyncResult(RunOutcome.SOURCE_BLOCKED, report, e.message ?: "La sorgente richiede un intervento.")
        }
        val succeeded = result.outcome == RunOutcome.COMPLETED || result.outcome == RunOutcome.MORE_WORK
        store.saveStatus(scope, SyncStatus(started, if (succeeded) now() else previous.lastSuccessAt, result.outcome, result.message))
        return result
    }

    private suspend fun execute(started: Instant): SyncResult {
        val status = source.status()
        if (status != SourceStatus.Ready) return SyncResult(RunOutcome.SOURCE_BLOCKED, report, status.message)

        var checkpoint = store.checkpoint(scope, provider) ?: Checkpoint(provider)
        if (checkpoint.historyYears != plan.historyYears) {
            // A different history setting restarts the search for older data; what is covered stays covered.
            checkpoint = checkpoint.copy(historyYears = plan.historyYears, emptyDays = 0, exhausted = false)
        }
        val floor = started.minus(Duration.ofDays(365L * (if (plan.historyYears == SyncPlan.ALL_HISTORY) limits.maxHistoryYears else plan.historyYears)))

        while (true) {
            val block = nextBlock(checkpoint, started, floor)
                ?: return SyncResult(RunOutcome.COMPLETED, report, "Sincronizzazione completata: ${report.describe()}")
            if (paused(started)) return moreWork()
            checkpoint = checkpoint.copy(blockStart = block.start, blockEnd = block.end, resumeAt = block.resumeAt)
            store.saveCheckpoint(scope, checkpoint)

            var handled = 0
            var token: String? = null
            // The workout at the resume position is read again: handling it twice is harmless.
            val readFrom = block.resumeAt ?: block.start
            val remote = RemoteLookup(block)
            reading@ while (true) {
                progress(SyncProgress("Lettura da ${provider.label}: ${WorkoutFormat.day(block.start)} – ${WorkoutFormat.day(block.end)}", report))
                val page = source.read(readFrom, block.end, token, limits.pageSize)
                for (workout in page.workouts) {
                    if (paused(started)) return moreWork()
                    when (val step = handle(workout, remote)) {
                        Step.Continue -> handled++
                        is Step.Halt -> return SyncResult(step.outcome, report, step.message)
                    }
                }
                // Everything up to the last workout of the page is confirmed: an interrupted block resumes from here.
                page.workouts.lastOrNull()?.let {
                    checkpoint = checkpoint.copy(resumeAt = it.cursor)
                    store.saveCheckpoint(scope, checkpoint)
                }
                token = page.nextToken ?: break@reading
                if (paused(started)) return moreWork()
            }

            checkpoint = completed(checkpoint, block, handled)
            store.saveCheckpoint(scope, checkpoint)
            report = report.copy(blocks = report.blocks + 1)
        }
    }

    private fun nextBlock(c: Checkpoint, started: Instant, floor: Instant): Block? {
        val from = c.coveredFrom
        val until = c.coveredUntil
        return when {
            // A block left unfinished comes first, from the position it reached.
            c.blockStart != null && c.blockEnd != null ->
                Block(c.blockStart, c.blockEnd, c.resumeAt, backward = from == null || !c.blockEnd.isAfter(from))
            // First run: the most recent block.
            from == null || until == null -> Block(maxOf(floor, started.minus(days(c.blockDays))), started, null, backward = true)
            // Forwards: what happened since the last run, plus the days that may still have changed.
            until.isBefore(started) -> {
                val start = maxOf(from, until.minus(days(limits.revisitDays)))
                Block(start, minOf(started, start.plus(days(limits.maxBlockDays))), null, backward = false)
            }
            // Backwards: older history, until the configured depth or until the source has nothing more.
            !c.exhausted && from.isAfter(floor) -> Block(maxOf(floor, from.minus(days(c.blockDays))), from, null, backward = true)
            else -> null
        }
    }

    private fun completed(c: Checkpoint, block: Block, handled: Int): Checkpoint {
        var next = c.copy(
            coveredFrom = minOf(c.coveredFrom ?: block.start, block.start),
            coveredUntil = maxOf(c.coveredUntil ?: block.end, block.end),
            blockStart = null, blockEnd = null, resumeAt = null)
        if (!block.backward) return next
        // A resumed block already held workouts before the interruption.
        val empty = handled == 0 && block.resumeAt == null
        val emptyDays = if (empty) c.emptyDays + Duration.between(block.start, block.end).toDays().toInt().coerceAtLeast(1) else 0
        next = next.copy(
            emptyDays = emptyDays,
            exhausted = plan.historyYears == SyncPlan.ALL_HISTORY && emptyDays >= limits.stopAfterEmptyDays,
            blockDays = when {
                handled > limits.shrinkAbove -> (c.blockDays / 2).coerceAtLeast(limits.minBlockDays)
                handled < limits.growBelow -> (c.blockDays * 2).coerceAtMost(limits.maxBlockDays)
                else -> c.blockDays
            })
        return next
    }

    private fun handle(w: SourceWorkout, remote: RemoteLookup): Step {
        val at = now()
        val payload = UploadMapper.toPayload(w)
        if (payload == null) {
            store.record(scope, LedgerEntry(provider, w.sourceId.take(128), w.revision, "", ItemState.INVALID, w.start, w.end,
                detail = "Identificativo o durata non validi nella sorgente.", updatedAt = at))
            report = report.copy(problems = report.problems + 1)
            return Step.Continue
        }
        val json = UploadMapper.json(payload)
        val fingerprint = UploadMapper.fingerprint(json)
        val known = store.entry(scope, provider, w.sourceId)
        if (known != null && known.fingerprint == fingerprint && !known.forced) {
            when (known.state) {
                ItemState.SYNCED -> { report = report.copy(unchanged = report.unchanged + 1); return Step.Continue }
                // Same content, same answer: asking again would only waste battery and data.
                ItemState.INVALID, ItemState.CONFLICT, ItemState.TOO_LARGE -> { report = report.copy(problems = report.problems + 1); return Step.Continue }
                ItemState.DUPLICATE_SUSPECT -> { report = report.copy(duplicates = report.duplicates + 1); return Step.Continue }
                ItemState.FAILED -> Unit
            }
        }

        fun entry(state: ItemState, detail: String? = null, serverId: String? = known?.serverId) = LedgerEntry(provider, w.sourceId, w.revision,
            fingerprint, state, w.start, w.end, serverId, detail?.take(MAX_DETAIL), (known?.attempts ?: 0) + 1, at)

        val neverAccepted = known == null || known.state == ItemState.FAILED || known.state == ItemState.DUPLICATE_SUSPECT
        if (neverAccepted && known?.forced != true) {
            val twin = when (val lookup = remote.workouts()) {
                is ReadResult.Ok -> DuplicateGuard.twin(w.start, w.end, provider.code, w.sourceId, lookup.value)
                is ReadResult.Failed -> return Step.Halt(
                    if (lookup.unauthorized) RunOutcome.AUTH_REQUIRED else RunOutcome.RETRY_LATER, lookup.message)
            }
            if (twin != null) {
                store.record(scope, entry(ItemState.DUPLICATE_SUSPECT,
                    "Il server ha già un allenamento sovrapposto, arrivato da ${WorkoutFormat.source(twin.source)}.", twin.id))
                report = report.copy(duplicates = report.duplicates + 1)
                return Step.Continue
            }
        }

        progress(SyncProgress("Invio dell'allenamento del ${WorkoutFormat.day(w.start)}", report))
        val result = server.upload(json)
        return when (result.outcome) {
            UploadOutcome.CREATED, UploadOutcome.STORED -> {
                // Recorded only now that the server has confirmed: a crash before this line means one harmless resend.
                store.record(scope, entry(ItemState.SYNCED, serverId = result.serverId ?: known?.serverId))
                report = if (result.outcome == UploadOutcome.CREATED) report.copy(created = report.created + 1) else report.copy(updated = report.updated + 1)
                Step.Continue
            }
            UploadOutcome.INVALID, UploadOutcome.CONFLICT, UploadOutcome.TOO_LARGE -> {
                val state = when (result.outcome) {
                    UploadOutcome.INVALID -> ItemState.INVALID
                    UploadOutcome.CONFLICT -> ItemState.CONFLICT
                    else -> ItemState.TOO_LARGE
                }
                store.record(scope, entry(state, result.detail))
                report = report.copy(problems = report.problems + 1)
                Step.Continue
            }
            UploadOutcome.UNAUTHORIZED -> Step.Halt(RunOutcome.AUTH_REQUIRED, result.detail ?: "Token non accettato dal server.")
            UploadOutcome.RETRY -> {
                store.record(scope, entry(ItemState.FAILED, result.detail))
                report = report.copy(failed = report.failed + 1)
                Step.Halt(RunOutcome.RETRY_LATER, "Server non raggiungibile o occupato: ${result.detail ?: "nuovo tentativo più tardi"}.")
            }
        }
    }

    /** Server summaries around a block, asked at most once and only when an unknown workout needs the check. */
    private inner class RemoteLookup(private val block: Block) {
        private var cached: ReadResult<List<RemoteWorkout>>? = null

        fun workouts(): ReadResult<List<RemoteWorkout>> = cached?.takeIf { it is ReadResult.Ok }
            ?: server.workoutsBetween(block.start.minus(DuplicateGuard.MARGIN), block.end.plus(DuplicateGuard.MARGIN)).also { cached = it }
    }

    private fun paused(started: Instant) = shouldStop() || Duration.between(started, now()).toMillis() >= limits.maxRunMillis

    private fun moreWork() = SyncResult(RunOutcome.MORE_WORK, report, "Sincronizzazione in corso, riprende automaticamente: ${report.describe()}")

    private fun days(count: Int) = Duration.ofDays(count.toLong())

    private companion object {
        const val MAX_DETAIL = 300
    }
}
