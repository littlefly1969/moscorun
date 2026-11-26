package it.moscorun.app.sync

import android.database.sqlite.SQLiteDatabase
import android.database.sqlite.SQLiteOpenHelper
import it.moscorun.app.model.Provider
import kotlinx.coroutines.runBlocking
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertNull
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.RobolectricTestRunner
import org.robolectric.RuntimeEnvironment
import java.time.Instant

@RunWith(RobolectricTestRunner::class)
class SqliteSyncStateStoreTest : SyncStateStoreContract() {
    private val context get() = RuntimeEnvironment.getApplication()
    private val sqlite = SqliteSyncStateStore(RuntimeEnvironment.getApplication(), "contract.db")
    override val store: SyncStateStore get() = sqlite

    @After fun close() = sqlite.close()

    @Test fun `state survives closing and reopening the database, as after a process death`() {
        val source = FakeSource(workouts = (1L..40L).map { workout("w$it", daysAgo(it)) })
        val server = FakeServer()
        SqliteSyncStateStore(context, "restart.db").use { first ->
            runBlocking { SyncEngine(source, server, first, "s", SyncPlan(1), now = { NOW }, shouldStop = { server.uploads.size >= 10 }).run() }
        }

        SqliteSyncStateStore(context, "restart.db").use { reopened ->
            assertNotNull(reopened.checkpoint("s", Provider.SAMSUNG_HEALTH)!!.resumeAt)
            assertEquals(10, reopened.counts("s")[ItemState.SYNCED])
            val result = runBlocking { SyncEngine(source, server, reopened, "s", SyncPlan(1), now = { NOW }).run() }

            assertEquals(RunOutcome.COMPLETED, result.outcome)
            assertEquals(40, server.created)
            assertEquals(40, reopened.counts("s")[ItemState.SYNCED])
        }
    }

    @Test fun `scientific position round trips per scope and source and follows scope moves and clears`() {
        val at = Instant.parse("2026-11-20T08:00:00Z")
        assertNull(sqlite.scienceSyncedUntil("a", "health-connect"))

        sqlite.saveScienceSyncedUntil("a", "health-connect", at)
        sqlite.saveScienceSyncedUntil("a", "health-connect", at.plusSeconds(60))
        sqlite.saveScienceSyncedUntil("b", "health-connect", at)

        assertEquals(at.plusSeconds(60), sqlite.scienceSyncedUntil("a", "health-connect"))
        sqlite.moveScope("a", "c")
        assertNull(sqlite.scienceSyncedUntil("a", "health-connect"))
        assertEquals(at.plusSeconds(60), sqlite.scienceSyncedUntil("c", "health-connect"))
        sqlite.resetCheckpoints("c")
        assertNull(sqlite.scienceSyncedUntil("c", "health-connect"))
        sqlite.clearScope("b")
        assertNull(sqlite.scienceSyncedUntil("b", "health-connect"))
    }

    @Test fun `a database of the previous version is upgraded keeping its rows`() {
        val name = "upgrade.db"
        context.deleteDatabase(name)
        val previous = object : SQLiteOpenHelper(context, name, null, 1) {
            override fun onCreate(db: SQLiteDatabase) {
                db.execSQL("CREATE TABLE status (scope TEXT NOT NULL PRIMARY KEY, last_attempt_at INTEGER, last_success_at INTEGER, outcome TEXT, message TEXT)")
                db.execSQL("INSERT INTO status (scope, message) VALUES ('s', 'prima')")
            }

            override fun onUpgrade(db: SQLiteDatabase, oldVersion: Int, newVersion: Int) = Unit
        }
        previous.writableDatabase
        previous.close()

        SqliteSyncStateStore(context, name).use { upgraded ->
            assertEquals("prima", upgraded.status("s").message)
            upgraded.saveScienceSyncedUntil("s", "health-connect", Instant.EPOCH)
            assertEquals(Instant.EPOCH, upgraded.scienceSyncedUntil("s", "health-connect"))
        }
    }
}
