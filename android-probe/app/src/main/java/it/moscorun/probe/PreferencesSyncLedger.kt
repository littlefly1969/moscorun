package it.moscorun.probe

import android.content.Context

/** Ledger kept in private app preferences (excluded from backups by allowBackup=false). */
class PreferencesSyncLedger(context: Context) : SyncLedger {
    private val prefs = context.getSharedPreferences("moscorun-sync", Context.MODE_PRIVATE)
    override fun isSent(sourceId: String, fingerprint: String) = prefs.getString(sourceId, null) == fingerprint
    override fun markSent(sourceId: String, fingerprint: String) { prefs.edit().putString(sourceId, fingerprint).apply() }
    override fun clear() { prefs.edit().clear().apply() }
}
