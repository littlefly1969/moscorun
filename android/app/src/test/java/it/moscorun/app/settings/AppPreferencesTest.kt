package it.moscorun.app.settings

import it.moscorun.app.model.Provider
import it.moscorun.app.sync.SyncPlan
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNotEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

class AppPreferencesTest {
    private val store = MemoryPreferences()
    private val prefs = AppPreferences(store)

    @Test fun `defaults are samsung health, one year, four hours, no unattended sending`() {
        assertEquals(Provider.SAMSUNG_HEALTH, prefs.provider)
        assertEquals(1, prefs.historyYears)
        assertEquals(4, prefs.intervalHours)
        assertFalse(prefs.autoSync)
        assertFalse(prefs.unmeteredOnly)
        assertFalse(prefs.authBlocked)
        assertNull(prefs.accountId)
    }

    @Test fun `choices survive a restart`() {
        prefs.provider = Provider.HEALTH_CONNECT
        prefs.historyYears = SyncPlan.ALL_HISTORY
        prefs.intervalHours = 12
        prefs.autoSync = true
        prefs.unmeteredOnly = true

        val afterRestart = AppPreferences(store)

        assertEquals(Provider.HEALTH_CONNECT, afterRestart.provider)
        assertEquals(SyncPlan.ALL_HISTORY, afterRestart.historyYears)
        assertEquals(12, afterRestart.intervalHours)
        assertTrue(afterRestart.autoSync)
        assertTrue(afterRestart.unmeteredOnly)
    }

    @Test fun `values out of range fall back to something safe`() {
        prefs.intervalHours = 0
        assertEquals(1, prefs.intervalHours)
        prefs.intervalHours = 1000
        assertEquals(24, prefs.intervalHours)
        prefs.historyYears = 99
        assertEquals(5, prefs.historyYears)
        store.edit().putInt("historyYears", -3).putString("provider", "SCONOSCIUTO").commit()
        assertEquals(1, prefs.historyYears)
        assertEquals(Provider.SAMSUNG_HEALTH, prefs.provider)
    }

    @Test fun `forgetting the server keeps source and schedule`() {
        prefs.provider = Provider.HEALTH_CONNECT
        prefs.autoSync = true
        prefs.accountId = "a"
        prefs.deviceName = "Telefono"
        prefs.authBlocked = true

        prefs.forgetServer()

        assertNull(prefs.accountId)
        assertNull(prefs.deviceName)
        assertFalse(prefs.authBlocked)
        assertEquals(Provider.HEALTH_CONNECT, prefs.provider)
        assertTrue(prefs.autoSync)
    }

    @Test fun `the state partition depends on server and account and does not reveal the address`() {
        val scope = SyncScope.of(SERVER, "account-1")

        assertEquals(scope, SyncScope.of(SERVER, "account-1"))
        assertNotEquals(scope, SyncScope.of(SERVER, "account-2"))
        assertNotEquals(scope, SyncScope.of("https://altro.example", "account-1"))
        assertNotEquals(scope, SyncScope.of(SERVER, null))
        assertEquals(32, scope.length)
        assertFalse(scope.contains("moscorun"))
    }
}
