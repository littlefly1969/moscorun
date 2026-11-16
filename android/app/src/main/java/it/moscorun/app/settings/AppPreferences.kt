package it.moscorun.app.settings

import android.content.Context
import android.content.SharedPreferences
import it.moscorun.app.model.Provider
import it.moscorun.app.sync.SyncPlan
import java.security.MessageDigest

/**
 * Choices that are not secrets: source, schedule, history depth and the account the token belongs to.
 * Changing any of them never touches the server address or the token.
 */
class AppPreferences(private val prefs: SharedPreferences) {
    constructor(context: Context) : this(context.applicationContext.getSharedPreferences("moscorun-app", Context.MODE_PRIVATE))

    /** Samsung Health Data SDK unless the user chose otherwise. */
    var provider: Provider
        get() = Provider.fromName(prefs.getString(PROVIDER, null)) ?: Provider.DEFAULT
        set(value) { prefs.edit().putString(PROVIDER, value.name).apply() }

    /** Background synchronisation is off until the user turns it on: that choice is the consent to send data unattended. */
    var autoSync: Boolean
        get() = prefs.getBoolean(AUTO_SYNC, false)
        set(value) { prefs.edit().putBoolean(AUTO_SYNC, value).apply() }

    var intervalHours: Int
        get() = prefs.getInt(INTERVAL, DEFAULT_INTERVAL_HOURS).coerceIn(MIN_INTERVAL_HOURS, MAX_INTERVAL_HOURS)
        set(value) { prefs.edit().putInt(INTERVAL, value.coerceIn(MIN_INTERVAL_HOURS, MAX_INTERVAL_HOURS)).apply() }

    /** 1 to 5 years, or [SyncPlan.ALL_HISTORY] for everything the source still has. */
    var historyYears: Int
        get() = prefs.getInt(HISTORY, SyncPlan.DEFAULT_HISTORY_YEARS).takeIf { it in 0..SyncPlan.MAX_HISTORY_YEARS } ?: SyncPlan.DEFAULT_HISTORY_YEARS
        set(value) { prefs.edit().putInt(HISTORY, value.coerceIn(0, SyncPlan.MAX_HISTORY_YEARS)).apply() }

    /** Wait for Wi-Fi (or another unmetered network) before syncing in the background. */
    var unmeteredOnly: Boolean
        get() = prefs.getBoolean(UNMETERED, false)
        set(value) { prefs.edit().putBoolean(UNMETERED, value).apply() }

    /** Account the device token belongs to, as told by the server; null until the first successful check. */
    var accountId: String?
        get() = prefs.getString(ACCOUNT, null)
        set(value) { prefs.edit().putString(ACCOUNT, value).apply() }

    var deviceName: String?
        get() = prefs.getString(DEVICE, null)
        set(value) { prefs.edit().putString(DEVICE, value).apply() }

    /** The server refused the token: background runs stay quiet until the user replaces it. */
    var authBlocked: Boolean
        get() = prefs.getBoolean(AUTH_BLOCKED, false)
        set(value) { prefs.edit().putBoolean(AUTH_BLOCKED, value).apply() }

    /** Forgets what refers to a server; source, schedule and history depth stay as the user set them. */
    fun forgetServer() {
        prefs.edit().remove(ACCOUNT).remove(DEVICE).remove(AUTH_BLOCKED).apply()
    }

    companion object {
        const val DEFAULT_INTERVAL_HOURS = 4
        const val MIN_INTERVAL_HOURS = 1
        const val MAX_INTERVAL_HOURS = 24
        val INTERVAL_CHOICES = listOf(1, 2, 4, 8, 12, 24)

        private const val PROVIDER = "provider"
        private const val AUTO_SYNC = "autoSync"
        private const val INTERVAL = "intervalHours"
        private const val HISTORY = "historyYears"
        private const val UNMETERED = "unmeteredOnly"
        private const val ACCOUNT = "accountId"
        private const val DEVICE = "deviceName"
        private const val AUTH_BLOCKED = "authBlocked"
    }
}

/**
 * Partition of the sync state: one per server and account, so workouts of different people or servers are never
 * mixed. Only a digest is stored, not the address.
 */
object SyncScope {
    /** Used until the server tells who the token belongs to (first check still pending, or an older server). */
    const val UNKNOWN_ACCOUNT = "unknown"

    fun of(serverUrl: String, accountId: String?): String =
        MessageDigest.getInstance("SHA-256").digest("$serverUrl|${accountId ?: UNKNOWN_ACCOUNT}".toByteArray())
            .take(16).joinToString("") { "%02x".format(it) }
}
