package it.moscorun.app.science

import com.google.gson.JsonParser
import kotlinx.coroutines.runBlocking
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test
import java.time.Duration
import java.time.Instant

internal val SCIENCE_NOW: Instant = Instant.parse("2026-11-26T08:00:00Z")

internal class FakeScienceReader(
    var availability: ScienceAvailability = ScienceAvailability(ScienceMetric.entries.toSet()),
    private val observations: List<ScienceObservation> = emptyList(),
) : ScienceReader {
    override val source = "health-connect"
    val windows = mutableListOf<Pair<Instant, Instant>>()
    var failure: Exception? = null

    override suspend fun availability() = availability

    override suspend fun read(from: Instant, to: Instant, metrics: Set<ScienceMetric>): List<ScienceObservation> {
        failure?.let { throw it }
        windows += from to to
        return observations.filter { !it.time.isBefore(from) && it.time.isBefore(to) }
    }
}

/** Accepts everything except the external ids in [refused], and answers [outcome] when set. */
internal class FakeScienceServer : ScienceServer {
    val stored = mutableListOf<String>()
    val requests = mutableListOf<Int>()
    var refused = setOf<String>()
    var outcome: ScienceOutcome? = null

    override fun postObservations(json: String): ScienceUploadResult {
        val ids = JsonParser.parseString(json).asJsonObject.getAsJsonArray("samples").map { it.asJsonObject["externalId"].asString }
        requests += ids.size
        outcome?.let { return ScienceUploadResult(it, "risposta $it") }
        if (ids.any { it in refused }) return ScienceUploadResult(ScienceOutcome.INVALID, "samples[0].metric: non ammessa")
        val fresh = ids.filter { it !in stored }
        stored += fresh
        return ScienceUploadResult(ScienceOutcome.STORED, imported = fresh.size)
    }
}

internal class MemoryScienceStore : ScienceStateStore {
    val positions = mutableMapOf<Pair<String, String>, Instant>()
    override fun scienceSyncedUntil(scope: String, source: String) = positions[scope to source]
    override fun saveScienceSyncedUntil(scope: String, source: String, until: Instant) { positions[scope to source] = until }
}

internal fun observation(id: String, time: Instant) = ScienceObservation(id, 1, "hrvRmssd", "ms", 40.0, time,
    ScienceMethod.PROVIDER_REPORTED, "app", "HeartRateVariabilityRmssdRecord", "ms", 40.0)

class ScienceSyncTest {
    private val store = MemoryScienceStore()
    private val server = FakeScienceServer()

    private fun daysAgo(days: Long) = SCIENCE_NOW.minus(Duration.ofDays(days))

    private fun run(reader: ScienceReader, stop: () -> Boolean = { false }) =
        runBlocking { ScienceSync(reader, server, store, "s", now = { SCIENCE_NOW }, shouldStop = stop).run() }

    @Test fun `first run reads the last 30 days in weekly windows and records the confirmed position`() {
        val reader = FakeScienceReader(observations = listOf(observation("a", daysAgo(1)), observation("b", daysAgo(20)), observation("old", daysAgo(40))))

        val result = run(reader)

        assertEquals(ScienceState.COMPLETED, result.state)
        assertEquals(listOf("b", "a"), server.stored)
        assertEquals(2, result.imported)
        assertEquals(5, reader.windows.size)
        assertEquals(SCIENCE_NOW.minus(Duration.ofMinutes(5)), store.positions["s" to "health-connect"])
        assertEquals(daysAgo(30).minus(Duration.ofMinutes(5)), reader.windows.first().first)
    }

    @Test fun `with access to older data the first run goes back a year`() {
        val reader = FakeScienceReader(ScienceAvailability(ScienceMetric.entries.toSet(), history = true), listOf(observation("old", daysAgo(200))))

        run(reader)

        assertEquals(listOf("old"), server.stored)
    }

    @Test fun `later runs read again the last week to catch late corrections, without moving the position back`() {
        store.positions["s" to "health-connect"] = daysAgo(1)
        val reader = FakeScienceReader(observations = listOf(observation("late", daysAgo(6)), observation("too-old", daysAgo(9))))

        run(reader)

        assertEquals(listOf("late"), server.stored)
        assertEquals(daysAgo(8), reader.windows.first().first)
    }

    @Test fun `a refused observation is isolated and the others are stored`() {
        val all = (1..9).map { observation("o$it", daysAgo(2).plusSeconds(it.toLong())) }
        server.refused = setOf("o4")

        val result = run(FakeScienceReader(observations = all))

        assertEquals(ScienceState.COMPLETED, result.state)
        assertEquals(8, server.stored.size)
        assertEquals(1, result.rejected)
        assertTrue(result.message, result.message.contains("non ammessa"))
    }

    @Test fun `batches never exceed the server limit`() {
        val many = (0 until 1200).map { observation("m$it", daysAgo(3).plusSeconds(it.toLong())) }

        run(FakeScienceReader(observations = many))

        assertEquals(listOf(500, 500, 200), server.requests)
    }

    @Test fun `without permissions nothing is read and the reason is reported`() {
        val reader = FakeScienceReader(ScienceAvailability(emptySet(), message = "Nessun permesso"))

        val result = run(reader)

        assertEquals(ScienceState.SKIPPED, result.state)
        assertEquals("Nessun permesso", result.message)
        assertTrue(reader.windows.isEmpty())
    }

    @Test fun `server answers stop the run without advancing past unconfirmed windows`() {
        mapOf(ScienceOutcome.RETRY to ScienceState.RETRY_LATER, ScienceOutcome.UNAUTHORIZED to ScienceState.AUTH_REQUIRED,
            ScienceOutcome.UNSUPPORTED to ScienceState.SKIPPED).forEach { (answer, state) ->
            store.positions.clear()
            server.outcome = answer

            val result = run(FakeScienceReader(observations = listOf(observation("x", daysAgo(25)))))

            assertEquals(answer.name, state, result.state)
            assertNull(answer.name, store.positions["s" to "health-connect"])
        }
    }

    @Test fun `the time budget stops between windows and the next run continues`() {
        val reader = FakeScienceReader(observations = listOf(observation("a", daysAgo(1)), observation("b", daysAgo(29))))
        var reads = 0

        val first = run(reader) { reads++ >= 1 }
        val second = run(reader)

        assertEquals(ScienceState.MORE_WORK, first.state)
        assertEquals(ScienceState.COMPLETED, second.state)
        assertEquals(listOf("b", "a"), server.stored)
    }

    @Test fun `a reader failure skips the scientific part without throwing`() {
        val reader = FakeScienceReader().apply { failure = SecurityException("background") }

        assertEquals(ScienceState.SKIPPED, run(reader).state)
    }
}
