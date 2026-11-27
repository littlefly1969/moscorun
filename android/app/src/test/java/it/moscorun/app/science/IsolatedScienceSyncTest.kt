package it.moscorun.app.science

import kotlinx.coroutines.runBlocking
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Assert.assertNull
import org.junit.Test
import java.time.Duration
import java.time.Instant

class IsolatedScienceSyncTest {
    private val store = MemoryScienceStore()
    private val server = FakeScienceServer()
    private val end = SCIENCE_NOW.minusSeconds(300)
    private class Reader : ScienceReader {
        override val source = "health-connect"
        override val isolatedStreams = true
        override val supportedMetrics = setOf(ScienceMetric.WEIGHT, ScienceMetric.HEART_RATE)
        var failHeart = false
        val reads = mutableListOf<Triple<ScienceMetric, Instant, Instant>>()
        override suspend fun availability() = ScienceAvailability(supportedMetrics, history = true)
        override fun checkpoint(availability: ScienceAvailability) =
            ScienceCapabilities.checkpoint(source, "test", availability)
        override suspend fun read(from: Instant, to: Instant, metrics: Set<ScienceMetric>): List<ScienceObservation> {
            val group = metrics.single()
            reads += Triple(group, from, to)
            if (group == ScienceMetric.HEART_RATE && failHeart) error("unavailable")
            val at = SCIENCE_NOW.minusSeconds(3600)
            return if (!at.isBefore(from) && at.isBefore(to))
                listOf(observation(group.name, at).copy(metric = if (group == ScienceMetric.WEIGHT) "weight" else "heartRate"))
            else emptyList()
        }
    }

    private fun run(reader: Reader, stop: () -> Boolean = { false }) = runBlocking {
        ScienceSync(reader, server, store, "scope", ScienceLimits(historyDays = 15), now = { SCIENCE_NOW },
            shouldStop = stop).run()
    }

    @Test fun `a blocked heart rate stream does not block body composition`() {
        val reader = Reader().apply { failHeart = true }
        val result = run(reader)
        assertEquals(ScienceState.RETRY_LATER, result.state)
        assertEquals(listOf("WEIGHT"), server.stored)
        val diagnostic = result.diagnostic!!.metrics.associateBy { it.metric }
        assertEquals(true, diagnostic.getValue("heartRate").readError)
        assertEquals(0, diagnostic.getValue("heartRate").windowsRead)
        assertEquals(false, diagnostic.getValue("weight").readError)
        assertTrue(diagnostic.getValue("weight").windowsRead!! > 0)
        assertTrue(store.positions.keys.none { it.second.contains("HEART_RATE") })
        reader.failHeart = false
        assertEquals(ScienceState.COMPLETED, run(reader).state)
        assertTrue("HEART_RATE" in server.stored)
    }

    @Test fun `first windows read recent observations before backfilling`() {
        val reader = Reader()
        val result = run(reader)
        assertEquals(ScienceState.COMPLETED, result.state)
        assertEquals(2, reader.reads.take(2).map { it.first }.distinct().size)
        assertTrue(reader.reads.take(2).all { it.third == end })
        assertTrue(reader.reads.any { it.second == end.minus(Duration.ofDays(15)) })
        assertTrue(store.positions.keys.any { it.second.endsWith(":older") })
        assertTrue(store.positions.keys.any { it.second.endsWith(":newer") })
    }
}
