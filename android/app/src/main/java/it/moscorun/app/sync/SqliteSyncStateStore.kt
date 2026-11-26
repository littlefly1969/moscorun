package it.moscorun.app.sync

import android.content.ContentValues
import android.content.Context
import android.database.Cursor
import android.database.sqlite.SQLiteDatabase
import android.database.sqlite.SQLiteOpenHelper
import it.moscorun.app.model.Provider
import it.moscorun.app.science.ScienceStateStore
import java.time.Instant

/**
 * Synchronisation state in the app's private SQLite database (not included in backups: allowBackup is false).
 * It stores identifiers, fingerprints, instants and outcomes; routes and measurements are never written here.
 * Each public method is one statement or one transaction, so the file is consistent after any interruption.
 */
class SqliteSyncStateStore(context: Context, name: String? = DATABASE) : SyncStateStore, ScienceStateStore, AutoCloseable {
    private val helper = object : SQLiteOpenHelper(context.applicationContext, name, null, VERSION) {
        override fun onCreate(db: SQLiteDatabase) {
            db.execSQL("""CREATE TABLE ledger (
                scope TEXT NOT NULL, provider TEXT NOT NULL, source_id TEXT NOT NULL, revision INTEGER NOT NULL,
                fingerprint TEXT NOT NULL, state TEXT NOT NULL, started_at INTEGER, ended_at INTEGER, server_id TEXT,
                detail TEXT, attempts INTEGER NOT NULL, updated_at INTEGER NOT NULL, forced INTEGER NOT NULL DEFAULT 0,
                PRIMARY KEY (scope, provider, source_id))""")
            db.execSQL("CREATE INDEX ledger_state ON ledger (scope, state, updated_at)")
            db.execSQL("""CREATE TABLE checkpoint (
                scope TEXT NOT NULL, provider TEXT NOT NULL, covered_from INTEGER, covered_until INTEGER,
                block_start INTEGER, block_end INTEGER, resume_at INTEGER, block_days INTEGER NOT NULL,
                empty_days INTEGER NOT NULL, history_years INTEGER NOT NULL, exhausted INTEGER NOT NULL,
                PRIMARY KEY (scope, provider))""")
            db.execSQL("""CREATE TABLE status (
                scope TEXT NOT NULL PRIMARY KEY, last_attempt_at INTEGER, last_success_at INTEGER, outcome TEXT, message TEXT)""")
            createScience(db)
        }

        // Each version adds columns or tables; rows are never dropped by an upgrade.
        override fun onUpgrade(db: SQLiteDatabase, oldVersion: Int, newVersion: Int) {
            if (oldVersion < 2) createScience(db)
        }

        private fun createScience(db: SQLiteDatabase) {
            db.execSQL("""CREATE TABLE science_checkpoint (
                scope TEXT NOT NULL, source TEXT NOT NULL, synced_until INTEGER NOT NULL, PRIMARY KEY (scope, source))""")
        }
    }
    private val db: SQLiteDatabase get() = helper.writableDatabase

    override fun entry(scope: String, provider: Provider, sourceId: String): LedgerEntry? =
        db.query("ledger", null, "scope = ? AND provider = ? AND source_id = ?", arrayOf(scope, provider.name, sourceId), null, null, null)
            .use { if (it.moveToFirst()) ledgerEntry(it) else null }

    override fun record(scope: String, entry: LedgerEntry) {
        db.insertWithOnConflict("ledger", null, ContentValues().apply {
            put("scope", scope); put("provider", entry.provider.name); put("source_id", entry.sourceId)
            put("revision", entry.revision); put("fingerprint", entry.fingerprint); put("state", entry.state.name)
            put("started_at", entry.startedAt?.toEpochMilli()); put("ended_at", entry.endedAt?.toEpochMilli())
            put("server_id", entry.serverId); put("detail", entry.detail); put("attempts", entry.attempts)
            put("updated_at", entry.updatedAt.toEpochMilli()); put("forced", if (entry.forced) 1 else 0)
        }, SQLiteDatabase.CONFLICT_REPLACE)
    }

    override fun checkpoint(scope: String, provider: Provider): Checkpoint? =
        db.query("checkpoint", null, "scope = ? AND provider = ?", arrayOf(scope, provider.name), null, null, null).use {
            if (!it.moveToFirst()) null
            else Checkpoint(provider, it.instant("covered_from"), it.instant("covered_until"), it.instant("block_start"),
                it.instant("block_end"), it.instant("resume_at"), it.getInt(it.getColumnIndexOrThrow("block_days")),
                it.getInt(it.getColumnIndexOrThrow("empty_days")), it.getInt(it.getColumnIndexOrThrow("history_years")),
                it.getInt(it.getColumnIndexOrThrow("exhausted")) != 0)
        }

    override fun saveCheckpoint(scope: String, checkpoint: Checkpoint) {
        db.insertWithOnConflict("checkpoint", null, ContentValues().apply {
            put("scope", scope); put("provider", checkpoint.provider.name)
            put("covered_from", checkpoint.coveredFrom?.toEpochMilli()); put("covered_until", checkpoint.coveredUntil?.toEpochMilli())
            put("block_start", checkpoint.blockStart?.toEpochMilli()); put("block_end", checkpoint.blockEnd?.toEpochMilli())
            put("resume_at", checkpoint.resumeAt?.toEpochMilli()); put("block_days", checkpoint.blockDays)
            put("empty_days", checkpoint.emptyDays); put("history_years", checkpoint.historyYears)
            put("exhausted", if (checkpoint.exhausted) 1 else 0)
        }, SQLiteDatabase.CONFLICT_REPLACE)
    }

    override fun status(scope: String): SyncStatus =
        db.query("status", null, "scope = ?", arrayOf(scope), null, null, null).use {
            if (!it.moveToFirst()) SyncStatus()
            else SyncStatus(it.instant("last_attempt_at"), it.instant("last_success_at"),
                it.text("outcome")?.let { name -> RunOutcome.entries.firstOrNull { o -> o.name == name } }, it.text("message"))
        }

    override fun saveStatus(scope: String, status: SyncStatus) {
        db.insertWithOnConflict("status", null, ContentValues().apply {
            put("scope", scope); put("last_attempt_at", status.lastAttemptAt?.toEpochMilli())
            put("last_success_at", status.lastSuccessAt?.toEpochMilli()); put("outcome", status.outcome?.name)
            put("message", status.message)
        }, SQLiteDatabase.CONFLICT_REPLACE)
    }

    override fun counts(scope: String): Map<ItemState, Int> =
        db.rawQuery("SELECT state, COUNT(*) FROM ledger WHERE scope = ? GROUP BY state", arrayOf(scope)).use {
            buildMap {
                while (it.moveToNext()) {
                    val state = ItemState.entries.firstOrNull { s -> s.name == it.getString(0) } ?: continue
                    put(state, it.getInt(1))
                }
            }
        }

    override fun problems(scope: String, limit: Int): List<LedgerEntry> =
        db.query("ledger", null, "scope = ? AND state <> ?", arrayOf(scope, ItemState.SYNCED.name), null, null,
            "updated_at DESC", limit.toString()).use {
            buildList { while (it.moveToNext()) add(ledgerEntry(it)) }
        }

    override fun force(scope: String, provider: Provider, sourceId: String) {
        db.update("ledger", ContentValues().apply { put("forced", 1) }, "scope = ? AND provider = ? AND source_id = ?",
            arrayOf(scope, provider.name, sourceId))
    }

    override fun resetCheckpoints(scope: String) = transaction {
        TABLES_WITH_CHECKPOINTS.forEach { delete(it, "scope = ?", arrayOf(scope)) }
    }

    override fun scienceSyncedUntil(scope: String, source: String): Instant? =
        db.query("science_checkpoint", arrayOf("synced_until"), "scope = ? AND source = ?", arrayOf(scope, source), null, null, null)
            .use { if (it.moveToFirst()) it.instant("synced_until") else null }

    override fun saveScienceSyncedUntil(scope: String, source: String, until: Instant) {
        db.insertWithOnConflict("science_checkpoint", null, ContentValues().apply {
            put("scope", scope); put("source", source); put("synced_until", until.toEpochMilli())
        }, SQLiteDatabase.CONFLICT_REPLACE)
    }

    override fun moveScope(from: String, to: String) {
        if (from == to) return
        transaction {
            val occupied = rawQuery("SELECT (SELECT COUNT(*) FROM ledger WHERE scope = ?1) + (SELECT COUNT(*) FROM checkpoint WHERE scope = ?1)",
                arrayOf(to)).use { it.moveToFirst() && it.getInt(0) > 0 }
            if (occupied) return@transaction
            TABLES.forEach { table ->
                // Rows that only describe the run (status, scientific position) give way to those being moved.
                if (table == "status" || table == "science_checkpoint") delete(table, "scope = ?", arrayOf(to))
                update(table, ContentValues().apply { put("scope", to) }, "scope = ?", arrayOf(from))
            }
        }
    }

    override fun clearScope(scope: String) = transaction {
        TABLES.forEach { delete(it, "scope = ?", arrayOf(scope)) }
    }

    override fun clearAll() = transaction {
        TABLES.forEach { delete(it, null, null) }
    }

    override fun close() = helper.close()

    private fun transaction(block: SQLiteDatabase.() -> Unit) {
        val database = db
        database.beginTransaction()
        try {
            database.block()
            database.setTransactionSuccessful()
        } finally {
            database.endTransaction()
        }
    }

    private fun ledgerEntry(c: Cursor) = LedgerEntry(
        Provider.fromName(c.text("provider")) ?: Provider.DEFAULT, c.text("source_id").orEmpty(),
        c.getLong(c.getColumnIndexOrThrow("revision")), c.text("fingerprint").orEmpty(),
        ItemState.entries.firstOrNull { it.name == c.text("state") } ?: ItemState.FAILED,
        c.instant("started_at"), c.instant("ended_at"), c.text("server_id"), c.text("detail"),
        c.getInt(c.getColumnIndexOrThrow("attempts")), c.instant("updated_at") ?: Instant.EPOCH,
        c.getInt(c.getColumnIndexOrThrow("forced")) != 0)

    private fun Cursor.text(column: String): String? = getColumnIndexOrThrow(column).let { if (isNull(it)) null else getString(it) }
    private fun Cursor.instant(column: String): Instant? = getColumnIndexOrThrow(column).let { if (isNull(it)) null else Instant.ofEpochMilli(getLong(it)) }

    companion object {
        const val DATABASE = "moscorun-sync.db"
        private const val VERSION = 2
        private val TABLES = listOf("ledger", "checkpoint", "status", "science_checkpoint")
        private val TABLES_WITH_CHECKPOINTS = listOf("checkpoint", "science_checkpoint")
    }
}
