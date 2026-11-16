package it.moscorun.app.settings

import it.moscorun.app.model.Provider
import it.moscorun.app.server.Failure
import it.moscorun.app.server.ReadResult
import it.moscorun.app.sync.FakeServer
import it.moscorun.app.sync.FakeSource
import it.moscorun.app.sync.MemoryStore
import it.moscorun.app.sync.NOW
import it.moscorun.app.sync.SyncRunner
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

class ServerSetupTest {
    private val cipher = FakeCipher()
    private val settings = SecureSettings(MemoryPreferences(), cipher)
    private val prefs = AppPreferences(MemoryPreferences())
    private val server = FakeServer()
    private val asked = mutableListOf<Pair<String, String>>()
    private val factory = { url: String, token: String -> asked += url to token; server }
    private val runner = SyncRunner(settings, prefs, MemoryStore(), { FakeSource(Provider.SAMSUNG_HEALTH) }, factory, now = { NOW })
    private val setup = ServerSetup(settings, prefs, runner, factory) { NOW }

    @Test fun `a valid address and token are stored once the server accepts them`() {
        val outcome = setup.connect(" $SERVER/ ", " $TOKEN ")

        assertTrue(outcome is ServerSetup.Outcome.Connected)
        assertEquals(SERVER to TOKEN, asked.single())
        assertTrue(settings.configured)
        assertEquals(FakeServer.ACCOUNT, prefs.accountId)
        assertEquals("Telefono", prefs.deviceName)
    }

    @Test fun `mistakes in address or token are explained without calling the server`() {
        assertTrue(setup.connect("http://insecure.example", TOKEN) is ServerSetup.Outcome.Rejected)
        assertTrue(setup.connect("non un indirizzo", TOKEN) is ServerSetup.Outcome.Rejected)
        assertTrue(setup.connect(SERVER, "sbagliato") is ServerSetup.Outcome.Rejected)

        assertTrue(asked.isEmpty())
        assertFalse(settings.configured)
    }

    @Test fun `a token the server refuses is not stored`() {
        server.identity = FakeServer.unauthorized

        val outcome = setup.connect(SERVER, TOKEN) as ServerSetup.Outcome.Rejected

        assertTrue(outcome.unauthorized)
        assertFalse(settings.configured)
    }

    @Test fun `an unreachable server stores nothing`() {
        server.identity = FakeServer.offline

        assertTrue(setup.connect(SERVER, TOKEN) is ServerSetup.Outcome.Rejected)
        assertFalse(settings.configured)
    }

    @Test fun `an older moscorun server is accepted, an unrelated address is not`() {
        server.identity = ReadResult.Failed("404", Failure.NOT_FOUND)

        val older = setup.connect(SERVER, TOKEN) as ServerSetup.Outcome.Connected
        assertNull(older.identity)
        assertTrue(settings.configured)

        settings.forget()
        server.lookupFailure = ReadResult.Failed("404", Failure.NOT_FOUND)
        assertTrue(setup.connect(SERVER, TOKEN) is ServerSetup.Outcome.Rejected)
        assertFalse(settings.configured)
    }

    @Test fun `replacing the token verifies the new one first and clears the block`() {
        setup.connect(SERVER, TOKEN)
        prefs.authBlocked = true
        server.identity = FakeServer.unauthorized

        assertTrue(setup.replaceToken(OTHER_TOKEN) is ServerSetup.Outcome.Rejected)
        assertEquals("the working token is kept", TOKEN, (settings.tokenState() as TokenState.Available).token)

        server.identity = ReadResult.Ok(it.moscorun.app.server.DeviceIdentity("d", "Nuovo", "PhoneBridge", FakeServer.ACCOUNT))
        assertTrue(setup.replaceToken(OTHER_TOKEN) is ServerSetup.Outcome.Connected)
        assertEquals(OTHER_TOKEN, (settings.tokenState() as TokenState.Available).token)
        assertEquals(SERVER, settings.serverUrl)
        assertFalse(prefs.authBlocked)
        assertEquals("Nuovo", prefs.deviceName)
    }

    @Test fun `verifying the connection changes nothing and reports a revoked token`() {
        setup.connect(SERVER, TOKEN)

        assertTrue(setup.verify() is ServerSetup.Outcome.Connected)
        assertFalse(prefs.authBlocked)

        server.identity = FakeServer.unauthorized
        assertTrue((setup.verify() as ServerSetup.Outcome.Rejected).unauthorized)
        assertTrue(prefs.authBlocked)
        assertEquals(TOKEN, (settings.tokenState() as TokenState.Available).token)
    }

    @Test fun `an offline check does not block the schedule`() {
        setup.connect(SERVER, TOKEN)
        server.identity = FakeServer.offline

        assertTrue(setup.verify() is ServerSetup.Outcome.Rejected)
        assertFalse(prefs.authBlocked)
    }

    @Test fun `an unreadable stored token is reported without calling the server`() {
        setup.connect(SERVER, TOKEN)
        asked.clear()
        cipher.broken = true

        assertTrue((setup.verify() as ServerSetup.Outcome.Rejected).unauthorized)
        assertTrue(asked.isEmpty())
    }

    @Test fun `nothing can be verified or replaced before the first configuration`() {
        assertTrue(setup.verify() is ServerSetup.Outcome.Rejected)
        assertTrue(setup.replaceToken(TOKEN) is ServerSetup.Outcome.Rejected)
    }
}
