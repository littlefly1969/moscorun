package it.moscorun.app.sync

import it.moscorun.app.model.Provider
import it.moscorun.app.settings.MemoryPreferences
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

class LegacyLedgerTest {
    private val prefs = MemoryPreferences()
    private val store = MemoryStore()

    @Test fun `old entries become synchronised rows of the samsung source`() {
        prefs.edit().putString("uid-1:0", "aa").putString("uid-2:0", "bb").commit()

        val imported = LegacyLedger(prefs).importInto(store, "scope", NOW)

        assertEquals(2, imported)
        val entry = store.entry("scope", Provider.SAMSUNG_HEALTH, "uid-1:0")!!
        assertEquals("aa", entry.fingerprint)
        assertEquals(ItemState.SYNCED, entry.state)
        assertTrue(prefs.values.isEmpty())
    }

    @Test fun `the import happens once and never overwrites newer state`() {
        prefs.edit().putString("uid-1:0", "old").commit()
        store.record("scope", LedgerEntry(Provider.SAMSUNG_HEALTH, "uid-1:0", 7, "new", ItemState.SYNCED, null, null, updatedAt = NOW))

        LegacyLedger(prefs).importInto(store, "scope", NOW)
        val second = LegacyLedger(prefs).importInto(store, "scope", NOW)

        assertEquals("new", store.entry("scope", Provider.SAMSUNG_HEALTH, "uid-1:0")?.fingerprint)
        assertEquals(0, second)
    }

    @Test fun `values that are not fingerprints are discarded`() {
        prefs.edit().putInt("contatore", 3).putString("uid-1:0", "aa").commit()

        assertEquals(1, LegacyLedger(prefs).importInto(store, "scope", NOW))
        assertNull(store.entry("scope", Provider.SAMSUNG_HEALTH, "contatore"))
    }
}
