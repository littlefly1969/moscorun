package it.moscorun.app.sync

import android.content.Context
import androidx.work.BackoffPolicy
import androidx.work.Constraints
import androidx.work.ExistingPeriodicWorkPolicy
import androidx.work.ExistingWorkPolicy
import androidx.work.NetworkType
import androidx.work.OneTimeWorkRequestBuilder
import androidx.work.PeriodicWorkRequestBuilder
import androidx.work.WorkManager
import java.util.concurrent.TimeUnit

/** Hands the synchronisation to WorkManager, which persists it across restarts and app updates. */
object SyncScheduler {
    const val PERIODIC = "moscorun-sync"
    const val ONE_SHOT = "moscorun-sync-now"

    /** Applies the current choices: schedules, reschedules or removes the periodic work. Safe to call at every start. */
    fun apply(context: Context, schedule: SyncSchedule) {
        val manager = WorkManager.getInstance(context)
        if (!schedule.enabled) {
            manager.cancelUniqueWork(PERIODIC)
            return
        }
        val request = PeriodicWorkRequestBuilder<SyncWorker>(
            schedule.intervalHours.toLong(), TimeUnit.HOURS, schedule.flexMinutes.toLong(), TimeUnit.MINUTES)
            .setConstraints(constraints(schedule.unmeteredOnly, background = true))
            .setBackoffCriteria(BackoffPolicy.EXPONENTIAL, schedule.backoffMinutes.toLong(), TimeUnit.MINUTES)
            .build()
        // UPDATE keeps the existing schedule and its next run time when nothing relevant changed.
        manager.enqueueUniquePeriodicWork(PERIODIC, ExistingPeriodicWorkPolicy.UPDATE, request)
    }

    /**
     * One run as soon as there is a network: on request of the user, after the setup, and to continue a long
     * first synchronisation that did not fit in a single run. There is never more than one: by default an already
     * queued run is kept; a [continuation] is queued behind the run that asks for it; [replace] discards a run
     * that is waiting (for instance in backoff) so that a request of the user starts now.
     */
    fun runSoon(context: Context, unmeteredOnly: Boolean = false, delaySeconds: Long = 0, continuation: Boolean = false, replace: Boolean = false) {
        val request = OneTimeWorkRequestBuilder<SyncWorker>()
            .setConstraints(constraints(unmeteredOnly, background = false))
            .setBackoffCriteria(BackoffPolicy.EXPONENTIAL, 1, TimeUnit.MINUTES)
            .setInitialDelay(delaySeconds, TimeUnit.SECONDS)
            .build()
        WorkManager.getInstance(context).enqueueUniqueWork(ONE_SHOT,
            when {
                continuation -> ExistingWorkPolicy.APPEND_OR_REPLACE
                replace -> ExistingWorkPolicy.REPLACE
                else -> ExistingWorkPolicy.KEEP
            }, request)
    }

    fun cancelAll(context: Context) {
        WorkManager.getInstance(context).apply { cancelUniqueWork(PERIODIC); cancelUniqueWork(ONE_SHOT) }
    }

    private fun constraints(unmeteredOnly: Boolean, background: Boolean) = Constraints.Builder()
        .setRequiredNetworkType(if (unmeteredOnly) NetworkType.UNMETERED else NetworkType.CONNECTED)
        // Unattended runs wait for a battery that is not low; a run the user asked for does not.
        .setRequiresBatteryNotLow(background)
        .build()
}
