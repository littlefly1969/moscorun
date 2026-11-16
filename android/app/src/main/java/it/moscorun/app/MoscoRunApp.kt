package it.moscorun.app

import android.app.Application

/**
 * Process start, with or without an interface: after a restart of the phone or an update of the app the system
 * starts the process only to run the background work, and the schedule must already be the one the user chose.
 */
class MoscoRunApp : Application() {
    override fun onCreate() {
        super.onCreate()
        CrashLog.install(this)
        // Scheduling is idempotent; a failure here must never prevent the app from opening.
        runCatching { AppGraph.of(this).reschedule() }
    }
}
