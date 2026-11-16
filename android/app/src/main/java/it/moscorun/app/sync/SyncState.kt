package it.moscorun.app.sync

import it.moscorun.app.model.Provider
import java.time.Instant

/** What happened to one source workout the last time it was handled. */
enum class ItemState {
    /** The server confirmed it (200 or 201) with exactly this content. */
    SYNCED,

    /** The server rejected the content (400): it is not sent again until the source changes it. */
    INVALID,

    /** The server holds different content with a revision that is not lower (409). */
    CONFLICT,

    /** Too large for the server (413). */
    TOO_LARGE,

    /** The server already has an overlapping workout from another source: held back until the user decides. */
    DUPLICATE_SUSPECT,

    /** A temporary failure: sent again at the next run. */
    FAILED,
}

/**
 * One row of the ledger. It holds identities, fingerprints and outcomes, never health measurements.
 * [fingerprint] is the SHA-256 of the JSON the server accepted or refused.
 */
data class LedgerEntry(
    val provider: Provider,
    val sourceId: String,
    val revision: Long,
    val fingerprint: String,
    val state: ItemState,
    val startedAt: Instant?,
    val endedAt: Instant?,
    val serverId: String? = null,
    val detail: String? = null,
    val attempts: Int = 1,
    val updatedAt: Instant,
    /** Set by the user from the diagnostics screen: send it even though it looks like a duplicate. */
    val forced: Boolean = false,
)

/**
 * How far the history of one provider has been read. The covered interval grows forwards at every run and
 * backwards during the first synchronisation; [resumeAt] marks progress inside an unfinished block.
 */
data class Checkpoint(
    val provider: Provider,
    /** Everything from here to [coveredUntil] has been read and confirmed; null before the first block completes. */
    val coveredFrom: Instant? = null,
    val coveredUntil: Instant? = null,
    /** Block being read when the run stopped, and the position reached inside it. */
    val blockStart: Instant? = null,
    val blockEnd: Instant? = null,
    val resumeAt: Instant? = null,
    /** Current block size in days: adapts to how many workouts a period holds. */
    val blockDays: Int = SyncLimits.DEFAULT_BLOCK_DAYS,
    /** Consecutive days without any workout at the old end of the history ("as far back as the source goes"). */
    val emptyDays: Int = 0,
    /** History setting the two fields above refer to: changing it restarts the search for older data. */
    val historyYears: Int = 0,
    val exhausted: Boolean = false,
)

enum class RunOutcome {
    /** Nothing left to do for now. */
    COMPLETED,

    /** Stopped by the time budget or by the system with history still to read: it continues at the next run. */
    MORE_WORK,

    /** Network or server trouble: the system retries with backoff. */
    RETRY_LATER,

    /** The server refuses the token: nothing is sent until the user replaces it. */
    AUTH_REQUIRED,

    /** Another run is in progress in this process: this one did nothing. */
    BUSY,

    /** No server or token yet: the initial setup is not complete. */
    NOT_CONFIGURED,

    /** The source cannot be read without the user (missing permission, consent, app update). */
    SOURCE_BLOCKED,
}

/** Last attempt and last success, shown on the home screen. Contains no health data. */
data class SyncStatus(
    val lastAttemptAt: Instant? = null,
    val lastSuccessAt: Instant? = null,
    val outcome: RunOutcome? = null,
    val message: String? = null,
)

/** Counters of one run. */
data class SyncReport(
    val created: Int = 0, val updated: Int = 0, val unchanged: Int = 0, val problems: Int = 0,
    val duplicates: Int = 0, val failed: Int = 0, val blocks: Int = 0,
) {
    val sent: Int get() = created + updated

    fun describe(): String = buildString {
        append("$created nuovi, $updated aggiornati o già presenti, $unchanged invariati")
        if (duplicates > 0) append(", $duplicates possibili doppioni in attesa")
        if (problems > 0) append(", $problems non accettati")
        if (failed > 0) append(", $failed da ritentare")
        append('.')
    }
}

data class SyncResult(val outcome: RunOutcome, val report: SyncReport, val message: String)

/**
 * Durable state of the synchronisation. Every write is a single atomic operation, so an interruption at any
 * point leaves a state the next run can continue from. [scope] separates servers and accounts: rows of one
 * scope are never read while another one is active.
 */
interface SyncStateStore {
    fun entry(scope: String, provider: Provider, sourceId: String): LedgerEntry?
    fun record(scope: String, entry: LedgerEntry)
    fun checkpoint(scope: String, provider: Provider): Checkpoint?
    fun saveCheckpoint(scope: String, checkpoint: Checkpoint)
    fun status(scope: String): SyncStatus
    fun saveStatus(scope: String, status: SyncStatus)
    fun counts(scope: String): Map<ItemState, Int>

    /** Entries that are not synchronised, most recent first. */
    fun problems(scope: String, limit: Int): List<LedgerEntry>

    /** Lets a held-back or refused workout be sent at the next run. */
    fun force(scope: String, provider: Provider, sourceId: String)

    /** Forgets how far the history was read, keeping the ledger: everything is read again, unchanged workouts are not resent. */
    fun resetCheckpoints(scope: String)

    /** Moves the state of [from] under [to] when the same account gets a better identity; no-op if [to] has data. */
    fun moveScope(from: String, to: String)
    fun clearScope(scope: String)
    fun clearAll()
}

/** Bounds that keep one run within memory, time and battery budgets. */
data class SyncLimits(
    /** Workouts read from the source and kept in memory at once. */
    val pageSize: Int = 4,
    val maxBlockDays: Int = MAX_BLOCK_DAYS,
    val minBlockDays: Int = 1,
    /** A block with more workouts than this is too coarse a checkpoint: the next ones are shorter. */
    val shrinkAbove: Int = 80,
    val growBelow: Int = 20,
    /** Stay well inside the ten minutes the system grants to background work. */
    val maxRunMillis: Long = 7 * 60_000L,
    /** Recently finished workouts may still be edited or completed by the watch: this many days are read again. */
    val revisitDays: Int = 7,
    /** "All the history": stop after this many consecutive days without a workout. */
    val stopAfterEmptyDays: Int = 365,
    /** Hard floor for "all the history". */
    val maxHistoryYears: Int = 20,
) {
    companion object {
        const val MAX_BLOCK_DAYS = 90
        const val DEFAULT_BLOCK_DAYS = 30
    }
}

/** User choices that shape a run. [historyYears] 0 means as far back as the source has data. */
data class SyncPlan(val historyYears: Int = DEFAULT_HISTORY_YEARS) {
    init { require(historyYears in 0..MAX_HISTORY_YEARS) { "Storico fuori intervallo." } }

    companion object {
        const val DEFAULT_HISTORY_YEARS = 1
        const val MAX_HISTORY_YEARS = 5
        const val ALL_HISTORY = 0
    }
}
