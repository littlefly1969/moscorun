package it.moscorun.app.sync

import it.moscorun.app.model.Provider
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test
import java.time.Instant

/** Behaviour every [SyncStateStore] must have; run against the in-memory reference and the SQLite store. */
abstract class SyncStateStoreContract {
    protected abstract val store: SyncStateStore
    private val at = Instant.parse("2026-11-16T08:00:00Z")

    private fun entry(id: String, state: ItemState = ItemState.SYNCED, provider: Provider = Provider.SAMSUNG_HEALTH, minute: Long = 0) =
        LedgerEntry(provider, id, 7, "fp-$id", state, at.minusSeconds(3600), at.minusSeconds(60), "server-$id", "dettaglio", 2, at.plusSeconds(minute * 60))

    @Test fun `ledger entries round trip and are replaced by identity`() {
        store.record("a", entry("w1"))
        store.record("a", entry("w1").copy(revision = 8, fingerprint = "fp2", state = ItemState.CONFLICT, serverId = null, detail = null, forced = true))

        val read = store.entry("a", Provider.SAMSUNG_HEALTH, "w1")!!

        assertEquals(entry("w1").copy(revision = 8, fingerprint = "fp2", state = ItemState.CONFLICT, serverId = null, detail = null, forced = true), read)
        assertNull(store.entry("a", Provider.HEALTH_CONNECT, "w1"))
        assertNull(store.entry("b", Provider.SAMSUNG_HEALTH, "w1"))
        assertEquals(mapOf(ItemState.CONFLICT to 1), store.counts("a"))
    }

    @Test fun `entries without times are kept`() {
        val legacy = LedgerEntry(Provider.SAMSUNG_HEALTH, "old", 0, "fp", ItemState.SYNCED, null, null, updatedAt = at)

        store.record("a", legacy)

        assertEquals(legacy, store.entry("a", Provider.SAMSUNG_HEALTH, "old"))
    }

    @Test fun `checkpoint round trips per provider`() {
        val full = Checkpoint(Provider.HEALTH_CONNECT, at.minusSeconds(900), at, at.minusSeconds(2000), at.minusSeconds(900),
            at.minusSeconds(1500), 15, 120, 3, true)
        store.saveCheckpoint("a", full)
        store.saveCheckpoint("a", Checkpoint(Provider.SAMSUNG_HEALTH))

        assertEquals(full, store.checkpoint("a", Provider.HEALTH_CONNECT))
        assertEquals(Checkpoint(Provider.SAMSUNG_HEALTH), store.checkpoint("a", Provider.SAMSUNG_HEALTH))
        assertNull(store.checkpoint("b", Provider.HEALTH_CONNECT))
    }

    @Test fun `status round trips and starts empty`() {
        assertEquals(SyncStatus(), store.status("a"))

        val status = SyncStatus(at, at.minusSeconds(30), RunOutcome.RETRY_LATER, "Server non raggiungibile")
        store.saveStatus("a", status)

        assertEquals(status, store.status("a"))
        assertEquals(SyncStatus(), store.status("b"))
    }

    @Test fun `problems list what is not synchronised, most recent first`() {
        store.record("a", entry("ok"))
        store.record("a", entry("old", ItemState.INVALID, minute = 1))
        store.record("a", entry("new", ItemState.DUPLICATE_SUSPECT, Provider.HEALTH_CONNECT, minute = 5))
        store.record("a", entry("mid", ItemState.FAILED, minute = 3))
        store.record("b", entry("other", ItemState.CONFLICT, minute = 9))

        assertEquals(listOf("new", "mid", "old"), store.problems("a", 10).map { it.sourceId })
        assertEquals(listOf("new", "mid"), store.problems("a", 2).map { it.sourceId })
        assertEquals(mapOf(ItemState.SYNCED to 1, ItemState.INVALID to 1, ItemState.DUPLICATE_SUSPECT to 1, ItemState.FAILED to 1), store.counts("a"))
    }

    @Test fun `force marks only the chosen entry`() {
        store.record("a", entry("w1", ItemState.DUPLICATE_SUSPECT))
        store.record("a", entry("w2", ItemState.DUPLICATE_SUSPECT))

        store.force("a", Provider.SAMSUNG_HEALTH, "w1")
        store.force("a", Provider.SAMSUNG_HEALTH, "missing")

        assertTrue(store.entry("a", Provider.SAMSUNG_HEALTH, "w1")!!.forced)
        assertTrue(!store.entry("a", Provider.SAMSUNG_HEALTH, "w2")!!.forced)
    }

    @Test fun `resetting checkpoints keeps the ledger`() {
        store.record("a", entry("w1"))
        store.saveCheckpoint("a", Checkpoint(Provider.SAMSUNG_HEALTH, at, at))
        store.saveCheckpoint("b", Checkpoint(Provider.SAMSUNG_HEALTH, at, at))

        store.resetCheckpoints("a")

        assertNull(store.checkpoint("a", Provider.SAMSUNG_HEALTH))
        assertEquals(entry("w1"), store.entry("a", Provider.SAMSUNG_HEALTH, "w1"))
        assertEquals(at, store.checkpoint("b", Provider.SAMSUNG_HEALTH)!!.coveredFrom)
    }

    @Test fun `state moves to a new scope only when that scope is empty`() {
        store.record("unknown", entry("w1"))
        store.saveCheckpoint("unknown", Checkpoint(Provider.SAMSUNG_HEALTH, at, at))
        store.saveStatus("unknown", SyncStatus(at, at, RunOutcome.COMPLETED, "ok"))

        store.moveScope("unknown", "account")

        assertNull(store.entry("unknown", Provider.SAMSUNG_HEALTH, "w1"))
        assertEquals(entry("w1"), store.entry("account", Provider.SAMSUNG_HEALTH, "w1"))
        assertEquals(at, store.checkpoint("account", Provider.SAMSUNG_HEALTH)!!.coveredFrom)
        assertEquals("ok", store.status("account").message)

        // A second account already has its own state: nothing of the first one may leak into it.
        store.record("other", entry("x1"))
        store.moveScope("account", "other")

        assertEquals(entry("w1"), store.entry("account", Provider.SAMSUNG_HEALTH, "w1"))
        assertNull(store.entry("other", Provider.SAMSUNG_HEALTH, "w1"))
    }

    @Test fun `clearing a scope leaves the others untouched`() {
        store.record("a", entry("w1")); store.saveCheckpoint("a", Checkpoint(Provider.SAMSUNG_HEALTH, at, at)); store.saveStatus("a", SyncStatus(at))
        store.record("b", entry("w1"))

        store.clearScope("a")

        assertNull(store.entry("a", Provider.SAMSUNG_HEALTH, "w1"))
        assertNull(store.checkpoint("a", Provider.SAMSUNG_HEALTH))
        assertEquals(SyncStatus(), store.status("a"))
        assertEquals(entry("w1"), store.entry("b", Provider.SAMSUNG_HEALTH, "w1"))

        store.clearAll()
        assertNull(store.entry("b", Provider.SAMSUNG_HEALTH, "w1"))
    }
}

class MemoryStoreTest : SyncStateStoreContract() {
    override val store: SyncStateStore = MemoryStore()
}
