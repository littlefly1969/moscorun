package it.moscorun.app.sync

import it.moscorun.app.model.Provider
import it.moscorun.app.server.UploadOutcome
import it.moscorun.app.server.UploadResult
import it.moscorun.app.source.SourceStatus
import kotlinx.coroutines.runBlocking
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test
import java.time.Duration

class SyncEngineTest {
    private val scope = "scope-a"
    private val store = MemoryStore()
    private val server = FakeServer()
    private var clock = NOW

    private fun engine(source: FakeSource, years: Int = 1, limits: SyncLimits = SyncLimits(), store: SyncStateStore = this.store,
        scope: String = this.scope, shouldStop: () -> Boolean = { false }) =
        SyncEngine(source, server, store, scope, SyncPlan(years), limits, { clock }, shouldStop)

    private fun run(engine: SyncEngine) = runBlocking { engine.run() }

    /** One run every [everyDays] days going back [days] days. */
    private fun history(days: Long, everyDays: Long = 3, provider: Provider = Provider.SAMSUNG_HEALTH) =
        (1..days step everyDays).map { workout("w$it", daysAgo(it).plusSeconds(3600), provider) }

    @Test fun `first run reads one year in blocks and sends every workout once`() {
        val source = FakeSource(workouts = history(400))

        val result = run(engine(source))

        val expected = source.workouts.filter { it.start.isAfter(daysAgo(365)) }.map { it.sourceId }
        assertEquals(RunOutcome.COMPLETED, result.outcome)
        assertEquals(expected.sorted(), server.storedIds.sorted())
        assertEquals(expected.size, result.report.created)
        assertEquals(expected.size, server.uploads.size)
        assertTrue("a year is not read in a single request", result.report.blocks > 3)
        assertTrue(source.reads.all { Duration.between(it.first, it.second) <= Duration.ofDays(SyncLimits.MAX_BLOCK_DAYS.toLong()) })
        assertEquals(daysAgo(365), store.checkpoint(scope, Provider.SAMSUNG_HEALTH)!!.coveredFrom)
        assertEquals(NOW, store.checkpoint(scope, Provider.SAMSUNG_HEALTH)!!.coveredUntil)
    }

    @Test fun `the most recent workouts are sent before the old ones`() {
        val source = FakeSource(workouts = history(300))

        run(engine(source))

        val firstTen = server.uploads.take(10).map { id -> source.workouts.first { it.sourceId == id }.start }
        assertTrue(firstTen.all { it.isAfter(daysAgo(SyncLimits.MAX_BLOCK_DAYS.toLong())) })
    }

    @Test fun `no more than one small page of workouts is held at a time`() {
        val source = FakeSource(workouts = history(365, everyDays = 1))

        run(engine(source, limits = SyncLimits(pageSize = 3)))

        assertEquals(3, source.largestPage)
        assertTrue(source.reads.all { it.third == 3 })
    }

    @Test fun `a second run sends nothing and only rereads the recent days`() {
        val source = FakeSource(workouts = history(200))
        run(engine(source))
        val uploads = server.uploads.size
        source.reads.clear()
        clock = NOW.plus(Duration.ofHours(4))

        val result = run(engine(source))

        assertEquals(RunOutcome.COMPLETED, result.outcome)
        assertEquals(uploads, server.uploads.size)
        assertEquals(0, result.report.sent)
        assertTrue(result.report.unchanged > 0)
        assertTrue("only the revisit window is read again", source.reads.all { !it.first.isBefore(NOW.minus(Duration.ofDays(7))) })
    }

    @Test fun `new and edited workouts are picked up by the next run`() {
        val source = FakeSource(workouts = history(60))
        run(engine(source))
        clock = NOW.plus(Duration.ofHours(4))
        source.workouts += workout("fresh", NOW.plus(Duration.ofHours(1)))
        val edited = source.workouts.first { it.sourceId == "w4" }
        source.workouts[source.workouts.indexOf(edited)] = edited.copy(revision = 2, distanceM = 7000.0)

        val result = run(engine(source))

        assertEquals(1, result.report.created)
        assertEquals(1, result.report.updated)
        assertEquals(2L, server.revisionOf("w4"))
        assertTrue("fresh" in server.storedIds)
    }

    @Test fun `history depth is configurable and extending it does not resend`() {
        val source = FakeSource(workouts = history(900, everyDays = 10))
        run(engine(source, years = 1))
        val afterOneYear = server.uploads.size
        assertTrue(source.workouts.filter { it.sourceId in server.storedIds }.all { it.start.isAfter(daysAgo(365)) })

        val result = run(engine(source, years = 2))

        assertEquals(RunOutcome.COMPLETED, result.outcome)
        assertEquals(source.workouts.count { it.start.isAfter(daysAgo(730)) }, server.storedIds.size)
        assertEquals("only the older year is sent", server.storedIds.size - afterOneYear, server.uploads.size - afterOneYear)
        assertEquals(server.uploads.size, server.uploads.distinct().size)
    }

    @Test fun `whole history goes back until the source has nothing more`() {
        // Three years of workouts with a gap of eight months, then nothing older.
        val workouts = history(1100, everyDays = 7).filterNot { it.start.isBefore(daysAgo(400)) && it.start.isAfter(daysAgo(640)) }
        val source = FakeSource(workouts = workouts)

        val result = run(engine(source, years = SyncPlan.ALL_HISTORY))

        assertEquals(RunOutcome.COMPLETED, result.outcome)
        assertEquals(workouts.size, server.storedIds.size)
        val checkpoint = store.checkpoint(scope, Provider.SAMSUNG_HEALTH)!!
        assertTrue(checkpoint.exhausted)
        assertTrue("stops a year after the oldest workout instead of reading twenty years",
            checkpoint.coveredFrom!!.isAfter(daysAgo(1100 + 365 + 2L * SyncLimits.MAX_BLOCK_DAYS)))
    }

    @Test fun `a run stopped by the system resumes without losing or duplicating workouts`() {
        val source = FakeSource(workouts = history(365, everyDays = 2))
        val total = source.workouts.size
        var runs = 0
        do {
            val before = server.uploads.size
            // The system stops the work after twenty uploads, in the middle of a block.
            val result = run(engine(source, shouldStop = { server.uploads.size - before >= 20 }))
            runs++
            assertTrue(result.outcome == RunOutcome.MORE_WORK || result.outcome == RunOutcome.COMPLETED)
        } while (result.outcome != RunOutcome.COMPLETED && runs < 100)

        assertTrue("needed several runs", runs > 5)
        assertEquals(total, server.storedIds.size)
        assertEquals("the server created each workout once", total, server.created)
        assertTrue("an interruption costs at most one page of resends per run", server.uploads.size <= total + runs * SyncLimits().pageSize)
    }

    @Test fun `an interrupted block resumes from its checkpoint instead of its start`() {
        val source = FakeSource(workouts = history(25, everyDays = 1))
        var allowed = 12
        run(engine(source, shouldStop = { server.uploads.size >= allowed }))
        val checkpoint = store.checkpoint(scope, Provider.SAMSUNG_HEALTH)!!
        assertNotNull(checkpoint.resumeAt)
        assertNull("the block is not covered until it is finished", checkpoint.coveredFrom)
        source.reads.clear()
        allowed = Int.MAX_VALUE

        run(engine(source))

        assertEquals(checkpoint.resumeAt, source.reads.first().first)
        assertEquals(source.workouts.size, server.created)
    }

    @Test fun `process death between the server answer and the ledger write causes one harmless resend`() {
        val source = FakeSource(workouts = history(30))
        store.failRecords = 1
        val crashed = runCatching { run(engine(source)) }
        assertTrue(crashed.isFailure)
        assertEquals(1, server.created)

        val result = run(engine(source))

        assertEquals(RunOutcome.COMPLETED, result.outcome)
        assertEquals(source.workouts.size, server.created)
        assertEquals(1, result.report.updated)
        assertEquals(source.workouts.size, server.storedIds.size)
    }

    @Test fun `network failure stops the run for a later retry and nothing is lost`() {
        val source = FakeSource(workouts = history(60))
        server.intercept = { _, _ -> if (server.uploads.size == 5) UploadResult(UploadOutcome.RETRY, "timeout") else null }

        val failed = run(engine(source))

        assertEquals(RunOutcome.RETRY_LATER, failed.outcome)
        assertEquals(1, store.counts(scope)[ItemState.FAILED])
        assertNull("the block with the failure is not marked as covered", store.checkpoint(scope, Provider.SAMSUNG_HEALTH)!!.coveredFrom)
        assertNull(store.status(scope).lastSuccessAt)

        server.intercept = { _, _ -> null }
        val result = run(engine(source))

        assertEquals(RunOutcome.COMPLETED, result.outcome)
        assertEquals(source.workouts.size, server.storedIds.size)
        assertNull(store.counts(scope)[ItemState.FAILED])
        assertEquals(clock, store.status(scope).lastSuccessAt)
    }

    @Test fun `a refused token stops immediately and marks nothing as sent`() {
        val source = FakeSource(workouts = history(60))
        server.intercept = { _, _ -> UploadResult(UploadOutcome.UNAUTHORIZED, "Token revocato") }

        val result = run(engine(source))

        assertEquals(RunOutcome.AUTH_REQUIRED, result.outcome)
        assertEquals(1, server.uploads.size)
        assertTrue(store.counts(scope).isEmpty())
        assertEquals(RunOutcome.AUTH_REQUIRED, store.status(scope).outcome)
    }

    @Test fun `refused workouts are recorded and not sent again until they change`() {
        val source = FakeSource(workouts = history(30))
        val refusals = mapOf("w1" to UploadOutcome.INVALID, "w4" to UploadOutcome.CONFLICT, "w7" to UploadOutcome.TOO_LARGE)
        server.intercept = { id, _ -> refusals[id]?.let { UploadResult(it, "motivo di $id") } }

        val first = run(engine(source))
        val uploads = server.uploads.size
        clock = NOW.plus(Duration.ofHours(4))
        val second = run(engine(source))

        assertEquals(RunOutcome.COMPLETED, first.outcome)
        assertEquals(3, first.report.problems)
        assertEquals(source.workouts.size - 3, server.storedIds.size)
        assertEquals(mapOf(ItemState.INVALID to "w1", ItemState.CONFLICT to "w4", ItemState.TOO_LARGE to "w7"),
            store.problems(scope, 10).associate { it.state to it.sourceId })
        assertEquals("motivo di w4", store.entry(scope, Provider.SAMSUNG_HEALTH, "w4")!!.detail)
        assertEquals("the same content is not offered again", uploads, server.uploads.size)
        assertEquals(3, second.report.problems)

        // The source corrects the workout: it is a new content and is tried again.
        server.intercept = { _, _ -> null }
        val fixed = source.workouts.first { it.sourceId == "w1" }
        source.workouts[source.workouts.indexOf(fixed)] = fixed.copy(revision = 2, distanceM = 5000.0)
        clock = clock.plus(Duration.ofHours(4))
        run(engine(source))

        assertEquals(ItemState.SYNCED, store.entry(scope, Provider.SAMSUNG_HEALTH, "w1")!!.state)
    }

    @Test fun `user can ask to retry a refused workout`() {
        val source = FakeSource(workouts = listOf(workout("w1", daysAgo(2))))
        server.intercept = { _, attempt -> if (attempt == 1) UploadResult(UploadOutcome.CONFLICT, "conflitto") else null }
        run(engine(source))

        store.force(scope, Provider.SAMSUNG_HEALTH, "w1")
        clock = NOW.plus(Duration.ofHours(1))
        run(engine(source))

        val entry = store.entry(scope, Provider.SAMSUNG_HEALTH, "w1")!!
        assertEquals(ItemState.SYNCED, entry.state)
        assertFalse(entry.forced)
        assertEquals(2, entry.attempts)
    }

    @Test fun `switching source does not import the same session twice`() {
        val samsung = FakeSource(Provider.SAMSUNG_HEALTH, history(60))
        run(engine(samsung))
        // Health Connect exposes the same sessions with other ids and slightly different times, plus one of its own.
        val viaHealthConnect = samsung.workouts.map { it.copy(provider = Provider.HEALTH_CONNECT, sourceId = "hc-${it.sourceId}",
            start = it.start.plusSeconds(20), end = it.end.minusSeconds(45), distanceM = it.distanceM!! + 37) }
        val onlyThere = workout("hc-only", daysAgo(5).plusSeconds(40_000), Provider.HEALTH_CONNECT)
        val healthConnect = FakeSource(Provider.HEALTH_CONNECT, viaHealthConnect + onlyThere)
        val lookupsBefore = server.lookups

        val result = run(engine(healthConnect))

        assertEquals(RunOutcome.COMPLETED, result.outcome)
        assertEquals(samsung.workouts.size, result.report.duplicates)
        assertEquals(1, result.report.created)
        assertEquals(samsung.workouts.size + 1, server.storedIds.size)
        val held = store.problems(scope, 100)
        assertTrue(held.all { it.state == ItemState.DUPLICATE_SUSPECT && it.detail!!.contains("Samsung Health") })
        assertTrue("the lookup is one request per block, not per workout", server.lookups - lookupsBefore <= result.report.blocks)
    }

    @Test fun `a suspected duplicate is kept visible and sent only when the user says so`() {
        val start = daysAgo(3)
        server.others += FakeServer.remote("samsung-health", "s1", start, start.plus(Duration.ofMinutes(40)))
        val source = FakeSource(Provider.HEALTH_CONNECT, listOf(workout("hc1", start.plusSeconds(30), Provider.HEALTH_CONNECT)))

        run(engine(source))
        clock = NOW.plus(Duration.ofHours(1))
        val again = run(engine(source))

        assertTrue(server.uploads.isEmpty())
        assertEquals(1, again.report.duplicates)
        assertEquals(ItemState.DUPLICATE_SUSPECT, store.entry(scope, Provider.HEALTH_CONNECT, "hc1")!!.state)

        store.force(scope, Provider.HEALTH_CONNECT, "hc1")
        clock = clock.plus(Duration.ofHours(1))
        run(engine(source))

        assertEquals(listOf("hc1"), server.storedIds)
        assertEquals(ItemState.SYNCED, store.entry(scope, Provider.HEALTH_CONNECT, "hc1")!!.state)
    }

    @Test fun `workouts that merely happen on the same day are not duplicates`() {
        val morning = daysAgo(3)
        server.others += FakeServer.remote("samsung-health", "s1", morning, morning.plus(Duration.ofMinutes(40)))
        // Starts when the other is almost over: ten minutes in common out of forty.
        val source = FakeSource(Provider.HEALTH_CONNECT, listOf(
            workout("hc-evening", morning.plus(Duration.ofHours(9)), Provider.HEALTH_CONNECT),
            workout("hc-adjacent", morning.plus(Duration.ofMinutes(30)), Provider.HEALTH_CONNECT)))

        val result = run(engine(source))

        assertEquals(2, result.report.created)
        assertEquals(0, result.report.duplicates)
    }

    @Test fun `a source that hands out new identifiers for old records does not double the history`() {
        val source = FakeSource(workouts = history(60))
        run(engine(source))
        // Samsung Health was restored on a new phone: same sessions, new record identifiers.
        val restored = FakeSource(workouts = source.workouts.map { it.copy(sourceId = "new-${it.sourceId}") })
        store.clearScope(scope)

        val result = run(engine(restored))

        assertEquals(0, result.report.sent)
        assertEquals(source.workouts.size, result.report.duplicates)
        assertEquals(source.workouts.size, server.storedIds.size)
    }

    @Test fun `a lost ledger is rebuilt from the server answers without duplicates`() {
        val source = FakeSource(workouts = history(60))
        run(engine(source))
        store.clearScope(scope)

        val result = run(engine(source))

        assertEquals("same identity: the server recognises each workout", source.workouts.size, result.report.updated)
        assertEquals(0, result.report.duplicates)
        assertEquals(source.workouts.size, server.created)
    }

    @Test fun `duplicate check that cannot reach the server postpones the run instead of guessing`() {
        val source = FakeSource(workouts = history(30))
        server.lookupFailure = FakeServer.offline

        val result = run(engine(source))

        assertEquals(RunOutcome.RETRY_LATER, result.outcome)
        assertTrue(server.uploads.isEmpty())

        server.lookupFailure = FakeServer.unauthorized
        assertEquals(RunOutcome.AUTH_REQUIRED, run(engine(source)).outcome)
    }

    @Test fun `a source that needs the user blocks the run with its explanation`() {
        val source = FakeSource(workouts = history(30))
        source.status = SourceStatus.NeedsPermission("Concedi a MoscoRun la lettura degli allenamenti.")

        val result = run(engine(source))

        assertEquals(RunOutcome.SOURCE_BLOCKED, result.outcome)
        assertEquals("Concedi a MoscoRun la lettura degli allenamenti.", result.message)
        assertTrue(source.reads.isEmpty())
        assertEquals(RunOutcome.SOURCE_BLOCKED, store.status(scope).outcome)
    }

    @Test fun `permission revoked while reading keeps what was done and resumes later`() {
        val source = FakeSource(workouts = history(365, everyDays = 2))
        source.blockAfterReads = 6

        val blocked = run(engine(source))
        val sent = server.created
        source.blockAfterReads = null
        val result = run(engine(source))

        assertEquals(RunOutcome.SOURCE_BLOCKED, blocked.outcome)
        assertEquals("Permesso revocato.", blocked.message)
        assertTrue(sent > 0)
        assertEquals(RunOutcome.COMPLETED, result.outcome)
        assertEquals(source.workouts.size, server.created)
    }

    @Test fun `time budget ends the run cleanly`() {
        val source = FakeSource(workouts = history(365, everyDays = 1))
        var ticks = 0
        val engine = SyncEngine(source, server, store, scope, SyncPlan(1), SyncLimits(maxRunMillis = 60_000),
            now = { NOW.plusSeconds(ticks++.toLong()) })

        val result = run(engine)

        assertEquals(RunOutcome.MORE_WORK, result.outcome)
        assertTrue(server.created in 1 until source.workouts.size)
        assertNotNull(store.status(scope).lastSuccessAt)
    }

    @Test fun `blocks shrink when a period is dense and grow when it is sparse`() {
        val dense = FakeSource(workouts = (0 until 4400).map { workout("d$it", daysAgo(1).minus(Duration.ofHours(it * 2L))) })
        run(engine(dense, limits = SyncLimits(shrinkAbove = 80, growBelow = 20)))
        assertTrue(store.checkpoint(scope, Provider.SAMSUNG_HEALTH)!!.blockDays < SyncLimits.DEFAULT_BLOCK_DAYS)

        val sparse = FakeSource(workouts = history(365, everyDays = 30))
        run(engine(sparse, scope = "scope-sparse"))
        assertEquals(SyncLimits.MAX_BLOCK_DAYS, store.checkpoint("scope-sparse", Provider.SAMSUNG_HEALTH)!!.blockDays)
    }

    @Test fun `workouts the server cannot identify are reported, not sent`() {
        val source = FakeSource(workouts = listOf(workout("id con spazi", daysAgo(2)), workout("ok", daysAgo(3))))

        val result = run(engine(source))

        assertEquals(RunOutcome.COMPLETED, result.outcome)
        assertEquals(listOf("ok"), server.storedIds)
        assertEquals(1, result.report.problems)
        assertEquals(ItemState.INVALID, store.problems(scope, 5).single().state)
    }

    @Test fun `state of different accounts never mixes`() {
        val source = FakeSource(workouts = history(30))
        run(engine(source, scope = "account-a"))
        val other = FakeServer()
        val result = runBlocking { SyncEngine(source, other, store, "account-b", SyncPlan(1), now = { clock }).run() }

        assertEquals("the second account receives everything although the first already has it", source.workouts.size, result.report.created)
        assertEquals(source.workouts.size, store.counts("account-a")[ItemState.SYNCED])
        assertEquals(source.workouts.size, store.counts("account-b")[ItemState.SYNCED])
    }

    @Test fun `status tells when the last attempt and the last success happened`() {
        val source = FakeSource(workouts = history(10))
        run(engine(source))
        val firstSuccess = store.status(scope).lastSuccessAt
        clock = NOW.plus(Duration.ofHours(4))
        source.status = SourceStatus.Unavailable("Samsung Health va aggiornato.")

        run(engine(source))

        val status = store.status(scope)
        assertEquals(NOW, firstSuccess)
        assertEquals(clock, status.lastAttemptAt)
        assertEquals("a failed attempt does not hide the last success", firstSuccess, status.lastSuccessAt)
        assertEquals("Samsung Health va aggiornato.", status.message)
    }
}
