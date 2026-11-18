package it.moscorun.app.sync

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class SyncScheduleTest {
    private fun schedule(configured: Boolean = true, auto: Boolean = true, blocked: Boolean = false, hours: Int = 4, unmetered: Boolean = false) =
        SyncSchedule.of(configured, auto, blocked, hours, unmetered)

    @Test fun `the default is about every four hours with a one hour window`() {
        val schedule = schedule()

        assertTrue(schedule.enabled)
        assertEquals(4, schedule.intervalHours)
        assertEquals(60, schedule.flexMinutes)
        assertFalse(schedule.unmeteredOnly)
    }

    @Test fun `nothing is scheduled without configuration, without consent or with a refused token`() {
        assertFalse(schedule(configured = false).enabled)
        assertFalse(schedule(auto = false).enabled)
        assertFalse(schedule(blocked = true).enabled)
    }

    @Test fun `the interval stays inside what the system accepts`() {
        assertEquals(1, schedule(hours = 0).intervalHours)
        assertEquals(24, schedule(hours = 500).intervalHours)
        assertEquals(15, schedule(hours = 1).flexMinutes)
        assertEquals(120, schedule(hours = 24).flexMinutes)
        // WorkManager refuses a flex window longer than the interval or shorter than five minutes.
        listOf(1, 2, 4, 8, 12, 24).forEach { assertTrue(schedule(hours = it).flexMinutes in 5..it * 60) }
    }

    @Test fun `wifi only is passed on`() {
        assertTrue(schedule(unmetered = true).unmeteredOnly)
    }

    private fun result(outcome: RunOutcome) = SyncResult(outcome, SyncReport(), "")

    @Test fun `a long history continues in further bounded runs`() {
        assertEquals(WorkerDecision.CONTINUE, WorkerDecision.after(result(RunOutcome.MORE_WORK), attempt = 0, maxAttempts = 5))
        assertEquals(WorkerDecision.CONTINUE, WorkerDecision.after(result(RunOutcome.MORE_WORK), attempt = 4, maxAttempts = 5))
    }

    @Test fun `temporary failures are retried with backoff a limited number of times`() {
        assertEquals(WorkerDecision.RETRY, WorkerDecision.after(result(RunOutcome.RETRY_LATER), attempt = 0, maxAttempts = 5))
        assertEquals(WorkerDecision.RETRY, WorkerDecision.after(result(RunOutcome.RETRY_LATER), attempt = 3, maxAttempts = 5))
        assertEquals(WorkerDecision.DONE, WorkerDecision.after(result(RunOutcome.RETRY_LATER), attempt = 4, maxAttempts = 5))
    }

    @Test fun `what needs the user is never retried in the background`() {
        listOf(RunOutcome.COMPLETED, RunOutcome.AUTH_REQUIRED, RunOutcome.SOURCE_BLOCKED, RunOutcome.NOT_CONFIGURED, RunOutcome.BUSY).forEach {
            assertEquals(WorkerDecision.DONE, WorkerDecision.after(result(it), attempt = 0, maxAttempts = 5))
        }
    }
}
