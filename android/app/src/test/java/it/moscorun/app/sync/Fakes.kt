package it.moscorun.app.sync

import com.google.gson.JsonParser
import it.moscorun.app.model.MetricSample
import it.moscorun.app.model.MetricSeries
import it.moscorun.app.model.Provider
import it.moscorun.app.model.SourceWorkout
import it.moscorun.app.model.TrackPoint
import it.moscorun.app.server.DeviceIdentity
import it.moscorun.app.server.Failure
import it.moscorun.app.server.ReadResult
import it.moscorun.app.server.RemoteWorkout
import it.moscorun.app.server.ServerApi
import it.moscorun.app.server.UploadOutcome
import it.moscorun.app.server.UploadResult
import it.moscorun.app.source.SourceBlockedException
import it.moscorun.app.source.SourcePage
import it.moscorun.app.source.SourceStatus
import it.moscorun.app.source.WorkoutSource
import java.time.Duration
import java.time.Instant
import java.util.UUID

internal val NOW: Instant = Instant.parse("2026-11-16T08:00:00Z")

internal fun daysAgo(days: Long, from: Instant = NOW): Instant = from.minus(Duration.ofDays(days))

internal fun workout(id: String, start: Instant, provider: Provider = Provider.SAMSUNG_HEALTH, revision: Long = 1,
    minutes: Long = 40, distance: Double = 6000.0) = SourceWorkout(provider, id, revision, "RUNNING", start, start.plus(Duration.ofMinutes(minutes)),
    utcOffsetMinutes = 60, distanceM = distance, averageHeartRate = 150.0,
    route = listOf(TrackPoint(start.toString(), 41.89, 12.49, 20.0, 4.0), TrackPoint(start.plusSeconds(1).toString(), 41.8901, 12.4901)),
    series = listOf(MetricSeries("HeartRate", listOf(MetricSample(start.toString(), 148.0)))))

/** A source holding a fixed list of workouts, paged like the real ones (oldest first, opaque tokens). */
internal class FakeSource(override val provider: Provider = Provider.SAMSUNG_HEALTH, workouts: List<SourceWorkout> = emptyList()) : WorkoutSource {
    val workouts = workouts.toMutableList()
    var status: SourceStatus = SourceStatus.Ready
    var blockAfterReads: Int? = null
    val reads = mutableListOf<Triple<Instant, Instant, Int>>()
    var largestPage = 0

    override suspend fun status() = status

    override suspend fun read(start: Instant, end: Instant, pageToken: String?, pageSize: Int): SourcePage {
        blockAfterReads?.let { if (reads.size >= it) throw SourceBlockedException("Permesso revocato.") }
        reads += Triple(start, end, pageSize)
        val matching = workouts.filter { !it.start.isBefore(start) && !it.start.isAfter(end) }.sortedBy { it.start }
        val from = pageToken?.toInt() ?: 0
        val page = matching.drop(from).take(pageSize)
        largestPage = maxOf(largestPage, page.size)
        return SourcePage(page, (from + pageSize).takeIf { it < matching.size }?.toString())
    }
}

/** A server that stores workouts with the same idempotency rules as the real one. */
internal class FakeServer : ServerApi {
    private data class Stored(val id: String, val revision: Long, val json: String)

    private val stored = linkedMapOf<Pair<String, String>, Stored>()
    val others = mutableListOf<RemoteWorkout>()
    val uploads = mutableListOf<String>()
    var created = 0
    var lookups = 0

    /** Decides the answer before the stored state is consulted; null lets the upload through. */
    var intercept: (sourceId: String, attempt: Int) -> UploadResult? = { _, _ -> null }
    var lookupFailure: ReadResult.Failed? = null
    var identity: ReadResult<DeviceIdentity> = ReadResult.Ok(DeviceIdentity("device", "Telefono", "PhoneBridge", ACCOUNT))

    val storedIds: List<String> get() = stored.keys.map { it.second }
    fun revisionOf(sourceId: String): Long? = stored.entries.firstOrNull { it.key.second == sourceId }?.value?.revision

    override fun identity() = identity

    override fun upload(json: String): UploadResult {
        val body = JsonParser.parseString(json).asJsonObject
        val key = body["source"].asString to body["sourceId"].asString
        uploads += key.second
        intercept(key.second, uploads.count { it == key.second })?.let { return it }
        val revision = body["revision"].asLong
        val existing = stored[key]
        return when {
            existing == null -> {
                created++
                Stored(UUID.randomUUID().toString(), revision, json).also { stored[key] = it }.let { UploadResult(UploadOutcome.CREATED, serverId = it.id) }
            }
            existing.json == json -> UploadResult(UploadOutcome.STORED, serverId = existing.id)
            revision > existing.revision -> {
                stored[key] = existing.copy(revision = revision, json = json)
                UploadResult(UploadOutcome.STORED, serverId = existing.id)
            }
            else -> UploadResult(UploadOutcome.CONFLICT, "Conflitto di revisione")
        }
    }

    override fun workoutsBetween(from: Instant, to: Instant): ReadResult<List<RemoteWorkout>> {
        lookups++
        lookupFailure?.let { return it }
        val own = stored.entries.map { (key, value) ->
            val body = JsonParser.parseString(value.json).asJsonObject
            remote(key.first, key.second, Instant.parse(body["startedAt"].asString), Instant.parse(body["endedAt"].asString), value.id)
        }
        return ReadResult.Ok((own + others).filter { !Instant.parse(it.startedAt).isBefore(from) && !Instant.parse(it.startedAt).isAfter(to) })
    }

    companion object {
        const val ACCOUNT = "0193a7b2-1c2d-7e8f-9a0b-1c2d3e4f5a6b"

        fun remote(source: String, sourceId: String, start: Instant, end: Instant, id: String = UUID.randomUUID().toString()) =
            RemoteWorkout(id, source, sourceId, "RUNNING", start.toString(), end.toString(), null, null, null, null, null, 0)

        val offline = ReadResult.Failed("Server non raggiungibile", Failure.OFFLINE)
        val unauthorized = ReadResult.Failed("Token non valido", Failure.UNAUTHORIZED)
    }
}

/** Reference implementation of the store contract, used where the SQLite one is not under test. */
internal class MemoryStore : SyncStateStore {
    private val ledger = mutableMapOf<Triple<String, Provider, String>, LedgerEntry>()
    private val checkpoints = mutableMapOf<Pair<String, Provider>, Checkpoint>()
    private val statuses = mutableMapOf<String, SyncStatus>()

    /** Makes the next ledger writes fail, as when the process dies right after the server answered. */
    var failRecords = 0

    override fun entry(scope: String, provider: Provider, sourceId: String) = ledger[Triple(scope, provider, sourceId)]

    override fun record(scope: String, entry: LedgerEntry) {
        if (failRecords > 0) { failRecords--; throw IllegalStateException("processo terminato") }
        ledger[Triple(scope, entry.provider, entry.sourceId)] = entry
    }

    override fun checkpoint(scope: String, provider: Provider) = checkpoints[scope to provider]
    override fun saveCheckpoint(scope: String, checkpoint: Checkpoint) { checkpoints[scope to checkpoint.provider] = checkpoint }
    override fun status(scope: String) = statuses[scope] ?: SyncStatus()
    override fun saveStatus(scope: String, status: SyncStatus) { statuses[scope] = status }
    override fun counts(scope: String) = ledger.filterKeys { it.first == scope }.values.groupingBy { it.state }.eachCount()
    override fun problems(scope: String, limit: Int) = ledger.filterKeys { it.first == scope }.values
        .filter { it.state != ItemState.SYNCED }.sortedByDescending { it.updatedAt }.take(limit)

    override fun force(scope: String, provider: Provider, sourceId: String) {
        ledger.computeIfPresent(Triple(scope, provider, sourceId)) { _, e -> e.copy(forced = true) }
    }

    override fun resetCheckpoints(scope: String) { checkpoints.keys.removeAll { it.first == scope } }

    override fun moveScope(from: String, to: String) {
        if (from == to || ledger.keys.any { it.first == to } || checkpoints.keys.any { it.first == to }) return
        ledger.filterKeys { it.first == from }.forEach { (k, v) -> ledger.remove(k); ledger[Triple(to, k.second, k.third)] = v }
        checkpoints.filterKeys { it.first == from }.forEach { (k, v) -> checkpoints.remove(k); checkpoints[to to k.second] = v }
        statuses.remove(from)?.let { statuses[to] = it }
    }

    override fun clearScope(scope: String) {
        ledger.keys.removeAll { it.first == scope }; checkpoints.keys.removeAll { it.first == scope }; statuses.remove(scope)
    }

    override fun clearAll() { ledger.clear(); checkpoints.clear(); statuses.clear() }
}
