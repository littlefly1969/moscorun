package it.moscorun.app.sync

import it.moscorun.app.settings.AppPreferences

/**
 * What the system is asked to do in the background, derived from the user's choices. Kept free of WorkManager
 * types so the policy can be tested on its own.
 *
 * The work is periodic and deferrable: Android decides the actual moment according to Doze, battery saver and
 * its own quotas, so the interval is a target and never a guarantee. There is no foreground service, no exact
 * alarm and no polling.
 */
data class SyncSchedule(
    /** False removes the periodic work: nothing is sent unattended. */
    val enabled: Boolean,
    val intervalHours: Int,
    /** The run may happen anywhere in the last part of each interval, which lets the system batch it with other work. */
    val flexMinutes: Int,
    val unmeteredOnly: Boolean,
    /** First delay before a failed run is tried again; it doubles at every further failure. */
    val backoffMinutes: Int = 15,
    /** After this many consecutive failed attempts the run gives up until the next period. */
    val maxAttempts: Int = 5,
) {
    companion object {
        fun from(configured: Boolean, prefs: AppPreferences): SyncSchedule = of(configured, prefs.autoSync, prefs.authBlocked, prefs.intervalHours, prefs.unmeteredOnly)

        fun of(configured: Boolean, autoSync: Boolean, authBlocked: Boolean, intervalHours: Int, unmeteredOnly: Boolean): SyncSchedule {
            val hours = intervalHours.coerceIn(AppPreferences.MIN_INTERVAL_HOURS, AppPreferences.MAX_INTERVAL_HOURS)
            return SyncSchedule(
                // With a refused token every run would fail the same way: the schedule resumes when it is replaced.
                enabled = configured && autoSync && !authBlocked,
                intervalHours = hours,
                flexMinutes = (hours * 60 / 4).coerceIn(15, 120),
                unmeteredOnly = unmeteredOnly)
        }
    }
}

/** What the background worker does after a run. */
enum class WorkerDecision {
    /** Nothing more to do until the next period (or until the user acts). */
    DONE,

    /** History is still being read: another bounded run follows shortly. */
    CONTINUE,

    /** Temporary failure: the system retries with exponential backoff. */
    RETRY;

    companion object {
        /** [attempt] counts the retries already made for this piece of work, starting from zero. */
        fun after(result: SyncResult, attempt: Int, maxAttempts: Int): WorkerDecision = when (result.outcome) {
            RunOutcome.MORE_WORK -> CONTINUE
            RunOutcome.RETRY_LATER -> if (attempt + 1 < maxAttempts) RETRY else DONE
            // Retrying cannot help when the user has to do something: the app explains it at the next opening.
            RunOutcome.COMPLETED, RunOutcome.NOT_CONFIGURED, RunOutcome.AUTH_REQUIRED, RunOutcome.SOURCE_BLOCKED, RunOutcome.BUSY -> DONE
        }
    }
}
