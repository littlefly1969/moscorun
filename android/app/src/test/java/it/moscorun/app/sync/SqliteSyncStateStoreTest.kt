package it.moscorun.app.sync

import it.moscorun.app.model.Provider
import kotlinx.coroutines.runBlocking
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotNull
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.RobolectricTestRunner
import org.robolectric.RuntimeEnvironment

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
}
