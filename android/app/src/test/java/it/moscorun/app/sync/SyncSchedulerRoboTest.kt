package it.moscorun.app.sync

import android.content.Context
import androidx.test.core.app.ApplicationProvider
import androidx.work.Configuration
import androidx.work.ListenableWorker
import androidx.work.NetworkType
import androidx.work.WorkInfo
import androidx.work.WorkManager
import androidx.work.testing.SynchronousExecutor
import androidx.work.testing.TestListenableWorkerBuilder
import androidx.work.testing.WorkManagerTestInitHelper
import kotlinx.coroutines.runBlocking
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.RobolectricTestRunner

/** The schedule as WorkManager really records it: unique, persistent, constrained and replaceable. */
@RunWith(RobolectricTestRunner::class)
class SyncSchedulerRoboTest {
    private val context: Context = ApplicationProvider.getApplicationContext()
    private lateinit var manager: WorkManager

    @Before fun setUp() {
        WorkManagerTestInitHelper.initializeTestWorkManager(context, Configuration.Builder().setExecutor(SynchronousExecutor()).build())
        manager = WorkManager.getInstance(context)
    }

    private fun works(name: String): List<WorkInfo> = manager.getWorkInfosForUniqueWork(name).get()
    private fun pending(name: String) = works(name).filter { !it.state.isFinished }
    private fun schedule(enabled: Boolean = true, hours: Int = 4, unmetered: Boolean = false) =
        SyncSchedule.of(configured = true, autoSync = enabled, authBlocked = false, intervalHours = hours, unmeteredOnly = unmetered)

    @Test fun `the periodic work is unique and waits for a network and a battery that is not low`() {
        SyncScheduler.apply(context, schedule())
        SyncScheduler.apply(context, schedule())

        val work = pending(SyncScheduler.PERIODIC).single()
        assertEquals(WorkInfo.State.ENQUEUED, work.state)
        assertEquals(NetworkType.CONNECTED, work.constraints.requiredNetworkType)
        assertTrue(work.constraints.requiresBatteryNotLow())
    }

    @Test fun `changing the settings updates the same work instead of adding another`() {
        SyncScheduler.apply(context, schedule())
        val first = pending(SyncScheduler.PERIODIC).single().id

        SyncScheduler.apply(context, schedule(hours = 12, unmetered = true))

        val work = pending(SyncScheduler.PERIODIC).single()
        assertEquals(first, work.id)
        assertEquals(NetworkType.UNMETERED, work.constraints.requiredNetworkType)
    }

    @Test fun `switching the automatic synchronisation off removes the periodic work`() {
        SyncScheduler.apply(context, schedule())

        SyncScheduler.apply(context, schedule(enabled = false))

        assertTrue(pending(SyncScheduler.PERIODIC).isEmpty())
    }

    @Test fun `asking twice for a run queues one run, without the battery constraint`() {
        SyncScheduler.runSoon(context)
        SyncScheduler.runSoon(context)

        val work = pending(SyncScheduler.ONE_SHOT).single()
        assertEquals(NetworkType.CONNECTED, work.constraints.requiredNetworkType)
        assertFalse(work.constraints.requiresBatteryNotLow())
    }

    @Test fun `a request of the user replaces a waiting run and there is still only one`() {
        SyncScheduler.runSoon(context)
        val waiting = pending(SyncScheduler.ONE_SHOT).single().id

        SyncScheduler.runSoon(context, replace = true)

        val work = pending(SyncScheduler.ONE_SHOT).single()
        assertFalse(waiting == work.id)
    }

    @Test fun `disconnecting cancels everything`() {
        SyncScheduler.apply(context, schedule())
        SyncScheduler.runSoon(context)

        SyncScheduler.cancelAll(context)

        assertTrue(pending(SyncScheduler.PERIODIC).isEmpty())
        assertTrue(pending(SyncScheduler.ONE_SHOT).isEmpty())
    }

    @Test fun `a background run on an app that is not configured ends quietly`() {
        val worker = TestListenableWorkerBuilder<SyncWorker>(context).build()

        val result = runBlocking { worker.doWork() }

        assertEquals(ListenableWorker.Result.success(), result)
        assertTrue("no continuation is queued", pending(SyncScheduler.ONE_SHOT).isEmpty())
    }
}
