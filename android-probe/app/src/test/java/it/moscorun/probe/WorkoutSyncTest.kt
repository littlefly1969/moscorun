package it.moscorun.probe

import com.google.gson.JsonParser
import org.junit.Assert.*
import org.junit.Test

class WorkoutSyncTest {
    private class MemoryLedger : SyncLedger {
        val sent = mutableMapOf<String, String>()
        override fun isSent(sourceId: String, fingerprint: String) = sent[sourceId] == fingerprint
        override fun markSent(sourceId: String, fingerprint: String) { sent[sourceId] = fingerprint }
        override fun clear() = sent.clear()
    }

    private fun workout(id: String, revision: Long = 1, uploadable: Boolean = true) =
        Workout(id, "RUNNING", "2026-11-14T07:00:00Z", "2026-11-14T08:00:00Z", "app", JsonParser.parseString("{}"),
            summary = if (uploadable) WorkoutSummary("RUNNING", revision) else null, uploadSource = "samsung-health")

    @Test fun `unchanged workouts are not sent twice and changed ones are`() {
        val ledger = MemoryLedger()
        val calls = mutableListOf<String>()
        val sync = WorkoutSync({ json -> calls += json; UploadResult(UploadOutcome.CREATED) }, ledger)

        val first = sync.run(listOf(workout("a:0"), workout("b:0"), workout("c:0", uploadable = false)))
        val second = sync.run(listOf(workout("a:0"), workout("b:0", revision = 2)))

        assertEquals(2, first.created); assertEquals(1, first.notUploadable)
        assertEquals(1, second.skipped); assertEquals(1, second.created)
        assertEquals(3, calls.size)
    }

    @Test fun `failures are reported and retried next time`() {
        val ledger = MemoryLedger()
        val failing = WorkoutSync({ UploadResult(UploadOutcome.CONFLICT, "revisione") }, ledger)
        val report = failing.run(listOf(workout("a:0")))

        assertEquals(1, report.failures.size)
        assertTrue(report.describe().contains("revisione"))
        assertTrue(ledger.sent.isEmpty())
    }

    @Test fun `authentication error stops the run`() {
        var calls = 0
        val report = WorkoutSync({ calls++; UploadResult(UploadOutcome.UNAUTHORIZED, "token") }, MemoryLedger())
            .run(listOf(workout("a:0"), workout("b:0"), workout("c:0")))

        assertEquals(1, calls)
        assertTrue(report.stoppedEarly)
    }
}
