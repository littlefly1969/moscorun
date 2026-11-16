package it.moscorun.app

import android.content.Context
import it.moscorun.app.model.Provider
import it.moscorun.app.server.MoscoRunServer
import it.moscorun.app.server.ServerApi
import it.moscorun.app.settings.AppPreferences
import it.moscorun.app.settings.SecureSettings
import it.moscorun.app.settings.TokenState
import it.moscorun.app.source.HealthConnectSource
import it.moscorun.app.source.SamsungHealthSource
import it.moscorun.app.source.WorkoutSource
import it.moscorun.app.sync.LegacyLedger
import it.moscorun.app.sync.SqliteSyncStateStore
import it.moscorun.app.sync.SyncProgress
import it.moscorun.app.sync.SyncRunner
import it.moscorun.app.sync.SyncSchedule
import it.moscorun.app.sync.SyncScheduler
import it.moscorun.app.sync.SyncStateStore

/**
 * The objects shared by the interface and the background worker, created once per process. Both connectors are
 * always present: which one is used is a setting, not a different build.
 */
class AppGraph private constructor(private val context: Context) {
    val settings = SecureSettings(context)
    val prefs = AppPreferences(context)
    val store: SyncStateStore = SqliteSyncStateStore(context)
    val samsung by lazy { SamsungHealthSource(context) }
    val healthConnect by lazy { HealthConnectSource(context) }
    val legacy = LegacyLedger(context)
    val sync = SyncRunner(settings, prefs, store, ::source, ::api, legacy)

    /** The only place where a client for a server address is created. */
    fun api(url: String, token: String): ServerApi = MoscoRunServer(url, token)

    /** What the run in progress is doing, for the home screen; null when idle. */
    @Volatile var liveProgress: SyncProgress? = null

    fun source(provider: Provider): WorkoutSource = when (provider) {
        Provider.SAMSUNG_HEALTH -> samsung
        Provider.HEALTH_CONNECT -> healthConnect
    }

    /** Client for the configured server, or null when the setup is incomplete or the token cannot be read. */
    fun server(): MoscoRunServer? {
        val url = settings.serverUrl ?: return null
        val token = (settings.tokenState() as? TokenState.Available)?.token ?: return null
        return MoscoRunServer(url, token)
    }

    /** Brings the background schedule in line with the current settings. */
    fun reschedule() = SyncScheduler.apply(context, SyncSchedule.from(settings.configured, prefs))

    /** Removes address, token and every trace of what was synchronised; source and schedule choices stay. */
    fun disconnect() {
        SyncScheduler.cancelAll(context)
        store.clearAll()
        legacy.clear()
        prefs.forgetServer()
        settings.forget()
    }

    companion object {
        @Volatile private var instance: AppGraph? = null

        fun of(context: Context): AppGraph = instance ?: synchronized(this) {
            instance ?: AppGraph(context.applicationContext).also { instance = it }
        }
    }
}
