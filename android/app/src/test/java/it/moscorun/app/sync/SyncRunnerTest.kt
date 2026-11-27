package it.moscorun.app.sync

import com.google.gson.JsonParser
import it.moscorun.app.model.Provider
import it.moscorun.app.science.FakeScienceReader
import it.moscorun.app.science.FakeScienceServer
import it.moscorun.app.science.MemoryScienceStore
import it.moscorun.app.science.ScienceOutcome
import it.moscorun.app.science.observation
import it.moscorun.app.server.DeviceIdentity
import it.moscorun.app.server.Failure
import it.moscorun.app.server.ReadResult
import it.moscorun.app.server.UploadOutcome
import it.moscorun.app.server.UploadResult
import it.moscorun.app.settings.AppPreferences
import it.moscorun.app.settings.FakeCipher
import it.moscorun.app.settings.MemoryPreferences
import it.moscorun.app.settings.OTHER_TOKEN
import it.moscorun.app.settings.SERVER
import it.moscorun.app.settings.SecureSettings
import it.moscorun.app.settings.SyncScope
import it.moscorun.app.settings.TOKEN
import it.moscorun.app.source.SourceStatus
import kotlinx.coroutines.runBlocking
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

class SyncRunnerTest {
    private val cipher = FakeCipher()
    private val settings = SecureSettings(MemoryPreferences(), cipher)
    private val prefs = AppPreferences(MemoryPreferences())
    private val store = MemoryStore()
    private val server = FakeServer()
    private val samsung = FakeSource(Provider.SAMSUNG_HEALTH, listOf(workout("s1", daysAgo(2)), workout("s2", daysAgo(40))))
    private val healthConnect = FakeSource(Provider.HEALTH_CONNECT)
    private val legacyPrefs = MemoryPreferences()
    private val tokens = mutableListOf<String>()

    private fun runner(legacy: LegacyLedger? = null) = SyncRunner(settings, prefs, store,
        { if (it == Provider.SAMSUNG_HEALTH) samsung else healthConnect }, { _, token -> tokens += token; server }, legacy, now = { NOW })

    private fun run(runner: SyncRunner = runner()) = runBlocking { runner.run() }

    private val scienceReader = FakeScienceReader(observations = listOf(observation("hc:1", daysAgo(1))))
    private val scienceServer = FakeScienceServer()
    private val sciencePositions = MemoryScienceStore()

    private fun scienceRunner() = SyncRunner(settings, prefs, store,
        { if (it == Provider.SAMSUNG_HEALTH) samsung else healthConnect }, { _, token -> tokens += token; server },
        now = { NOW }, science = { scienceReader }, scienceStore = sciencePositions)

    @Test fun `scientific data follows the workouts and its summary joins the run message`() {
        settings.save(SERVER, TOKEN)
        server.science = scienceServer

        val result = run(scienceRunner())

        assertEquals(RunOutcome.COMPLETED, result.outcome)
        assertEquals(listOf("s1", "s2"), server.storedIds.sorted())
        assertEquals(listOf("hc:1"), scienceServer.stored)
        assertNotNull(sciencePositions.scienceSyncedUntil(SyncScope.of(SERVER, FakeServer.ACCOUNT), "health-connect"))
        assertTrue(result.message, result.message.contains("Dati scientifici aggiornati"))
        assertEquals(result.message, store.status(SyncScope.of(SERVER, FakeServer.ACCOUNT)).message)
    }

    @Test fun `the run diagnostic reaches the server with counters only and no biometric values`() {
        settings.save(SERVER, TOKEN)
        server.science = scienceServer

        val result = run(scienceRunner())

        val report = JsonParser.parseString(scienceServer.diagnostics.single()).asJsonObject
        assertEquals("health-connect", report["source"].asString)
        assertEquals("COMPLETED", report["state"].asString)
        assertEquals(listOf("source", "runId", "startedAt", "finishedAt", "state", "firstReadFrom", "readThrough",
            "windowsRead", "metrics"), report.keySet().toList())
        val hrv = report.getAsJsonArray("metrics").map { it.asJsonObject }.single { it["metric"].asString == "hrvRmssd" }
        assertEquals(listOf("metric", "permission", "read", "attempted", "confirmed", "rejected", "conflicts"), hrv.keySet().toList())
        assertEquals("granted", hrv["permission"].asString)
        assertEquals(1, hrv["read"].asInt)
        assertEquals(1, hrv["confirmed"].asInt)
        assertTrue(result.message, result.message.contains("Diagnostica scientifica ricevuta dal server"))
    }

    @Test fun `a diagnostic the server does not confirm never changes the outcome of the run`() {
        settings.save(SERVER, TOKEN)
        server.science = scienceServer.apply { diagnosticOutcome = ScienceOutcome.RETRY }

        val result = run(scienceRunner())

        assertEquals(RunOutcome.COMPLETED, result.outcome)
        assertEquals(listOf("hc:1"), scienceServer.stored)
        assertTrue(result.message, result.message.contains("Diagnostica remota non confermata (RETRY)"))
    }

    @Test fun `no diagnostic is sent with a refused token`() {
        settings.save(SERVER, TOKEN)
        server.science = scienceServer.apply { outcome = ScienceOutcome.UNAUTHORIZED }

        run(scienceRunner())

        assertTrue(scienceServer.diagnostics.isEmpty())
    }

    @Test fun `scientific data waits while the workouts are not up to date`() {
        settings.save(SERVER, TOKEN)
        server.science = scienceServer
        server.intercept = { _, _ -> UploadResult(UploadOutcome.RETRY, "occupato") }

        val result = run(scienceRunner())

        assertEquals(RunOutcome.RETRY_LATER, result.outcome)
        assertTrue(scienceReader.windows.isEmpty())
        assertTrue(scienceServer.stored.isEmpty())
    }

    @Test fun `a server without scientific ingestion leaves the workouts result as it is`() {
        settings.save(SERVER, TOKEN)

        val result = run(scienceRunner())

        assertEquals(RunOutcome.COMPLETED, result.outcome)
        assertTrue(result.message, result.message.contains("aggiornarlo"))
        assertFalse(prefs.authBlocked)
    }

    @Test fun `a token refused for scientific data stops everything like a refused workout`() {
        settings.save(SERVER, TOKEN)
        server.science = scienceServer.apply { outcome = ScienceOutcome.UNAUTHORIZED }

        val result = run(scienceRunner())

        assertEquals(RunOutcome.AUTH_REQUIRED, result.outcome)
        assertTrue(prefs.authBlocked)
    }

    @Test fun `without a configuration nothing is read or sent`() {
        val result = run()

        assertEquals(RunOutcome.NOT_CONFIGURED, result.outcome)
        assertTrue(samsung.reads.isEmpty())
        assertTrue(server.uploads.isEmpty())
    }

    @Test fun `a configured app synchronises with the stored token and the chosen source`() {
        settings.save(SERVER, TOKEN)

        val result = run()

        assertEquals(RunOutcome.COMPLETED, result.outcome)
        assertEquals(listOf("s1", "s2"), server.storedIds.sorted())
        assertEquals(listOf(TOKEN), tokens)
        assertTrue(healthConnect.reads.isEmpty())
        assertEquals(FakeServer.ACCOUNT, prefs.accountId)
        assertEquals("Telefono", prefs.deviceName)
        assertNotNull(store.status(SyncScope.of(SERVER, FakeServer.ACCOUNT)).lastSuccessAt)
    }

    @Test fun `a second run after a restart sends nothing again`() {
        settings.save(SERVER, TOKEN)
        run()
        val sent = server.uploads.size

        val result = run(runner())

        assertEquals(RunOutcome.COMPLETED, result.outcome)
        assertEquals(sent, server.uploads.size)
    }

    @Test fun `a refused token stops the run and blocks the schedule until it is replaced`() {
        settings.save(SERVER, TOKEN)
        server.identity = FakeServer.unauthorized

        val result = run()

        assertEquals(RunOutcome.AUTH_REQUIRED, result.outcome)
        assertTrue(prefs.authBlocked)
        assertTrue(server.uploads.isEmpty())
        assertFalse(SyncSchedule.from(settings.configured, prefs.apply { autoSync = true }).enabled)

        settings.replaceToken(OTHER_TOKEN)
        server.identity = ReadResult.Ok(DeviceIdentity("device", "Telefono", "PhoneBridge", FakeServer.ACCOUNT))
        assertEquals(RunOutcome.COMPLETED, run().outcome)
        assertFalse(prefs.authBlocked)
        assertEquals(OTHER_TOKEN, tokens.last())
    }

    @Test fun `an unreadable token asks the user and sends nothing`() {
        settings.save(SERVER, TOKEN)
        cipher.broken = true

        val result = run()

        assertEquals(RunOutcome.AUTH_REQUIRED, result.outcome)
        assertTrue(tokens.isEmpty())
        assertEquals(RunOutcome.AUTH_REQUIRED, store.status(SyncScope.of(SERVER, null)).outcome)
    }

    @Test fun `an unreachable server is retried later and is not mistaken for a refused token`() {
        settings.save(SERVER, TOKEN)
        server.identity = FakeServer.offline

        val result = run()

        assertEquals(RunOutcome.RETRY_LATER, result.outcome)
        assertFalse(prefs.authBlocked)
        assertTrue(server.uploads.isEmpty())
    }

    @Test fun `a server older than the identity endpoint still synchronises`() {
        settings.save(SERVER, TOKEN)
        server.identity = ReadResult.Failed("Non disponibile", Failure.NOT_FOUND)

        val result = run()

        assertEquals(RunOutcome.COMPLETED, result.outcome)
        assertEquals(2, server.storedIds.size)
        assertNull(prefs.accountId)
    }

    @Test fun `state collected before the account was known follows it once the server tells`() {
        settings.save(SERVER, TOKEN)
        server.identity = ReadResult.Failed("Non disponibile", Failure.NOT_FOUND)
        run()
        val sent = server.uploads.size
        server.identity = ReadResult.Ok(DeviceIdentity("device", "Telefono", "PhoneBridge", FakeServer.ACCOUNT))

        val result = run()

        assertEquals(RunOutcome.COMPLETED, result.outcome)
        assertEquals("nothing is sent again after the server update", sent, server.uploads.size)
        assertEquals(2, store.counts(SyncScope.of(SERVER, FakeServer.ACCOUNT))[ItemState.SYNCED])
    }

    @Test fun `a token of another account never reuses the state of the first one`() {
        settings.save(SERVER, TOKEN)
        run()
        val other = "0193a7b2-0000-7000-8000-000000000002"
        server.identity = ReadResult.Ok(DeviceIdentity("device-2", "Altro", "PhoneBridge", other))
        settings.replaceToken(OTHER_TOKEN)

        run()

        assertEquals(other, prefs.accountId)
        assertEquals(2, store.counts(SyncScope.of(SERVER, FakeServer.ACCOUNT))[ItemState.SYNCED])
        assertEquals("the other account starts from its own empty ledger", 4, server.uploads.size)
    }

    @Test fun `changing source keeps server and token and holds back overlapping workouts`() {
        settings.save(SERVER, TOKEN)
        run()
        healthConnect.workouts += workout("hc-1", daysAgo(2), Provider.HEALTH_CONNECT)
        healthConnect.workouts += workout("hc-new", daysAgo(5), Provider.HEALTH_CONNECT)
        prefs.provider = Provider.HEALTH_CONNECT

        val result = run()

        assertEquals(RunOutcome.COMPLETED, result.outcome)
        assertEquals(listOf(TOKEN, TOKEN), tokens)
        assertEquals(listOf("hc-new", "s1", "s2"), server.storedIds.sorted())
        assertEquals(1, result.report.duplicates)
        assertEquals(ItemState.DUPLICATE_SUSPECT, store.entry(SyncScope.of(SERVER, FakeServer.ACCOUNT), Provider.HEALTH_CONNECT, "hc-1")?.state)
    }

    @Test fun `a source that needs the user stops the run with its reason`() {
        settings.save(SERVER, TOKEN)
        samsung.status = SourceStatus.NeedsPermission("Concedi il permesso.")

        val result = run()

        assertEquals(RunOutcome.SOURCE_BLOCKED, result.outcome)
        assertTrue(server.uploads.isEmpty())
    }

    @Test fun `the ledger of the previous app version is imported once and avoids resending`() {
        settings.save(SERVER, TOKEN)
        val known = samsung.workouts.first { it.sourceId == "s1" }
        legacyPrefs.edit().putString("s1", it.moscorun.app.server.UploadMapper.fingerprint(it.moscorun.app.server.UploadMapper.json(it.moscorun.app.server.UploadMapper.toPayload(known)!!))).commit()

        val result = run(runner(LegacyLedger(legacyPrefs)))

        assertEquals(RunOutcome.COMPLETED, result.outcome)
        assertEquals(listOf("s2"), server.uploads)
        assertTrue("imported entries are removed from the old file", legacyPrefs.values.isEmpty())
    }

    @Test fun `only one run at a time touches source and server`() {
        settings.save(SERVER, TOKEN)
        val runner = runner()
        var nested: SyncResult? = null
        val result = runBlocking {
            runner.run(progress = { if (nested == null) nested = runBlocking { runner.run() } })
        }

        assertEquals(RunOutcome.COMPLETED, result.outcome)
        assertEquals(RunOutcome.BUSY, nested?.outcome)
        assertFalse(runner.running)
    }
}
