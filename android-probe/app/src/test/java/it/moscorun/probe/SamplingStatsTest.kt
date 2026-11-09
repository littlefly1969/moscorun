package it.moscorun.probe

import org.junit.Assert.*
import org.junit.Test

class SamplingStatsTest {
    @Test fun `empty and single sample have no invented frequency`() {
        assertNull(SamplingStats.calculate(emptyList()).medianIntervalMs)
        val one=SamplingStats.calculate(listOf("2026-01-01T12:00:00Z"))
        assertEquals(1,one.count);assertNull(one.maxIntervalMs)
    }
    @Test fun `duplicates remain counted but do not create zero intervals`() {
        val times=listOf("2026-01-01T12:00:01Z","2026-01-01T12:00:00Z","2026-01-01T12:00:00Z","2026-01-01T12:00:03Z")
        val stats=SamplingStats.calculate(times)
        assertEquals(4,stats.count);assertEquals(3,stats.uniqueTimes)
        assertEquals(1500L,stats.medianIntervalMs);assertEquals(1000L,stats.minIntervalMs);assertEquals(2000L,stats.maxIntervalMs)
        assertEquals("2026-01-01T12:00:01Z",times.first())
    }
    @Test fun `irregular sampling and a large gap remain visible`() {
        val stats=SamplingStats.calculate(listOf("2026-01-01T00:00:00Z","2026-01-01T00:00:00.250Z","2026-01-01T00:00:01Z","2026-01-01T00:01:01Z"))
        assertEquals(750L,stats.medianIntervalMs);assertEquals(60000L,stats.maxIntervalMs)
    }
    @Test fun `equivalent instants with different offsets count as one time`() {
        val stats=SamplingStats.calculate(listOf("2026-01-01T12:00:00Z","2026-01-01T13:00:00+01:00"))
        assertEquals(2,stats.count);assertEquals(1,stats.uniqueTimes);assertNull(stats.medianIntervalMs)
    }
}
