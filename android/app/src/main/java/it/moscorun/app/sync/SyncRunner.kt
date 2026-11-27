package it.moscorun.app.sync

import android.content.Context
import android.content.SharedPreferences
import it.moscorun.app.model.Provider
import it.moscorun.app.science.ScienceLimits
import it.moscorun.app.science.ScienceOutcome
import it.moscorun.app.science.ScienceReader
import it.moscorun.app.science.ScienceState
import it.moscorun.app.science.ScienceStateStore
import it.moscorun.app.science.ScienceSync
import it.moscorun.app.server.DeviceIdentity
import it.moscorun.app.server.Failure
import it.moscorun.app.server.ReadResult
import it.moscorun.app.server.ServerApi
import it.moscorun.app.settings.AppPreferences
import it.moscorun.app.settings.SecureSettings
import it.moscorun.app.settings.SyncScope
import it.moscorun.app.settings.TokenState
import it.moscorun.app.source.WorkoutSource
import java.time.Duration
import java.time.Instant

/**
 * Ledger of the app versions before the durable sync: private preferences mapping the Samsung source id of each
 * workout to the fingerprint of what the server accepted. Importing it avoids sending the whole history again
 * after the update; the payload format, and therefore the fingerprint, is unchanged.
 */
class LegacyLedger(private val prefs: SharedPreferences) {
    constructor(context: Context) : this(context.applicationContext.getSharedPreferences("moscorun-sync", Context.MODE_PRIVATE))

    /** Copies the old entries into [store] and removes them, so this happens once. */
    fun importInto(store: SyncStateStore, scope: String, now: Instant): Int {
        val entries = prefs.all.filterValues { it is String }
        entries.forEach { (sourceId, fingerprint) ->
            if (store.entry(scope, Provider.SAMSUNG_HEALTH, sourceId) == null) {
                store.record(scope, LedgerEntry(Provider.SAMSUNG_HEALTH, sourceId, 0, fingerprint as String, ItemState.SYNCED,
                    startedAt = null, endedAt = null, detail = "Importato dalla versione precedente dell'app.", updatedAt = now))
            }
        }
        if (prefs.all.isNotEmpty()) prefs.edit().clear().commit()
        return entries.size
    }

    fun clear() { prefs.edit().clear().commit() }
}

/**
 * One synchronisation run from the stored configuration: checks the token, settles which account the state
 * belongs to, then lets [SyncEngine] do the work. Used both by the background worker and by "Sincronizza ora".
 */
class SyncRunner(
    private val settings: SecureSettings,
    private val prefs: AppPreferences,
    private val store: SyncStateStore,
    private val source: (Provider) -> WorkoutSource,
    private val server: (url: String, token: String) -> ServerApi,
    private val legacy: LegacyLedger? = null,
    private val limits: SyncLimits = SyncLimits(),
    private val now: () -> Instant = Instant::now,
    /** Daily scientific data, read after the workouts; null leaves the scientific part out. */
    private val science: (() -> ScienceReader)? = null,
    private val scienceStore: ScienceStateStore? = store as? ScienceStateStore,
    private val additionalScience: () -> List<ScienceReader> = { emptyList() },
) {
    /** State partition of the current configuration, or null when no server is configured. */
    fun scope(): String? = settings.serverUrl?.let { SyncScope.of(it, prefs.accountId) }

    /** True while a run is in progress in this process. */
    val running: Boolean get() = busy.get()

    suspend fun run(shouldStop: () -> Boolean = { false }, progress: (SyncProgress) -> Unit = {}): SyncResult {
        // The periodic work and a run asked by the user may be started together: only one touches source and server.
        if (!busy.compareAndSet(false, true)) return SyncResult(RunOutcome.BUSY, SyncReport(), "Sincronizzazione già in corso.")
        return try { execute(shouldStop, progress) } finally { busy.set(false) }
    }

    private suspend fun execute(shouldStop: () -> Boolean, progress: (SyncProgress) -> Unit): SyncResult {
        val url = settings.serverUrl
            ?: return SyncResult(RunOutcome.NOT_CONFIGURED, SyncReport(), "Server non configurato: completare la configurazione iniziale.")
        val token = when (val state = settings.tokenState()) {
            is TokenState.Available -> state.token
            TokenState.Unreadable -> return stopped(url, RunOutcome.AUTH_REQUIRED,
                "Il token salvato non è più leggibile su questo telefono: inserirlo di nuovo nelle impostazioni.")
            TokenState.Missing -> return SyncResult(RunOutcome.NOT_CONFIGURED, SyncReport(), "Token assente: completare la configurazione.")
        }
        val api = server(url, token)

        when (val identity = api.identity()) {
            is ReadResult.Ok -> adopt(url, identity.value)
            is ReadResult.Failed -> when {
                identity.unauthorized -> {
                    prefs.authBlocked = true
                    return stopped(url, RunOutcome.AUTH_REQUIRED, identity.message)
                }
                // A server older than the identity endpoint: keep working with the state already in use.
                identity.kind == Failure.NOT_FOUND -> Unit
                else -> return stopped(url, RunOutcome.RETRY_LATER, identity.message)
            }
        }
        val scope = SyncScope.of(url, prefs.accountId)
        legacy?.importInto(store, scope, now())

        val started = now()
        val readers = listOfNotNull(science?.invoke()) + additionalScience()
        val independent = readers.any { it.isolatedStreams } && scienceStore != null
        val workoutLimits = if (independent) limits.copy(maxRunMillis = limits.maxRunMillis / 2) else limits
        val workouts = SyncEngine(source(prefs.provider), api, store, scope, SyncPlan(prefs.historyYears), workoutLimits, now, shouldStop, progress).run()
        val scienceAllowed = workouts.outcome == RunOutcome.COMPLETED ||
            independent && workouts.outcome in setOf(RunOutcome.MORE_WORK, RunOutcome.SOURCE_BLOCKED)
        val result = if (scienceAllowed) withScience(workouts, readers, api, scope, started, shouldStop, progress) else workouts
        prefs.authBlocked = result.outcome == RunOutcome.AUTH_REQUIRED
        return result
    }

    /**
     * Standalone scientific observations use reserved time even while workout history is incomplete.
     * Each independent source gets a share of the remaining budget. Its problems never hide the
     * workouts' result, except a refused token, which stops everything.
     */
    private suspend fun withScience(workouts: SyncResult, readers: List<ScienceReader>, api: ServerApi, scope: String, started: Instant,
        shouldStop: () -> Boolean, progress: (SyncProgress) -> Unit): SyncResult {
        if (readers.isEmpty()) return workouts
        val positions = scienceStore ?: return workouts
        var combined = workouts
        for ((index, reader) in readers.withIndex()) {
            if (combined.outcome == RunOutcome.AUTH_REQUIRED) break
            val remaining = (limits.maxRunMillis - Duration.between(started, now()).toMillis()).coerceAtLeast(0)
            val deadline = now().plusMillis(remaining / (readers.size - index))
            val current = syncScienceReader(combined, reader, api, positions, scope, started,
                shouldStop, deadline, progress)
            combined = if (current.outcome == RunOutcome.AUTH_REQUIRED) current
                else if (combined.outcome in setOf(RunOutcome.MORE_WORK, RunOutcome.SOURCE_BLOCKED))
                    current.copy(outcome = combined.outcome) else current
        }
        store.saveStatus(scope, store.status(scope).copy(outcome = combined.outcome, message = combined.message))
        return combined
    }

    private suspend fun syncScienceReader(workouts: SyncResult, reader: ScienceReader, api: ServerApi, positions: ScienceStateStore,
        scope: String, started: Instant, shouldStop: () -> Boolean, deadline: Instant, progress: (SyncProgress) -> Unit): SyncResult {
        progress(SyncProgress("Invio dei dati scientifici: ${reader.source}", workouts.report))
        val historyYears = if (prefs.historyYears == SyncPlan.ALL_HISTORY) limits.maxHistoryYears else prefs.historyYears
        val scienceLimits = if (reader.isolatedStreams) ScienceLimits(historyDays = historyYears * 365) else ScienceLimits()
        val outcome = ScienceSync(reader, api, positions, scope, limits = scienceLimits, now = now,
            shouldStop = { shouldStop() || !now().isBefore(deadline) || Duration.between(started, now()).toMillis() >= limits.maxRunMillis }).run()
        val telemetry = if (outcome.state != ScienceState.AUTH_REQUIRED && !shouldStop())
            outcome.diagnostic?.let { api.postScienceDiagnostic(it.json()) }
        else null
        val telemetryMessage = when (telemetry) {
            ScienceOutcome.STORED -> "Diagnostica scientifica ricevuta dal server"
            ScienceOutcome.UNSUPPORTED -> "Diagnostica remota non supportata dal server"
            null -> "Diagnostica remota non inviata"
            else -> "Diagnostica remota non confermata (${telemetry.name})"
        }
        val message = listOf(workouts.message, reader.source + ": " + outcome.describe(), telemetryMessage).joinToString(". ") { it.trimEnd('.', ' ') } + "."
        val result = when (outcome.state) {
            ScienceState.AUTH_REQUIRED -> SyncResult(RunOutcome.AUTH_REQUIRED, workouts.report, outcome.message)
            ScienceState.MORE_WORK -> SyncResult(RunOutcome.MORE_WORK, workouts.report, message)
            ScienceState.RETRY_LATER -> if (reader.isolatedStreams) SyncResult(RunOutcome.MORE_WORK, workouts.report, message) else workouts.copy(message = message)
            else -> workouts.copy(message = message)
        }
        if (result.message != workouts.message) store.saveStatus(scope, store.status(scope).copy(outcome = result.outcome, message = result.message))
        return result
    }

    /**
     * Records who the token belongs to. The first time, the state collected before the account was known is
     * moved under it; a different account simply gets its own, empty partition.
     */
    fun adopt(url: String, identity: DeviceIdentity) {
        if (prefs.accountId == null) store.moveScope(SyncScope.of(url, null), SyncScope.of(url, identity.accountId))
        prefs.accountId = identity.accountId
        prefs.deviceName = identity.name
    }

    private fun stopped(url: String, outcome: RunOutcome, message: String): SyncResult {
        val scope = SyncScope.of(url, prefs.accountId)
        val at = now()
        store.saveStatus(scope, store.status(scope).copy(lastAttemptAt = at, outcome = outcome, message = message))
        return SyncResult(outcome, SyncReport(), message)
    }

    private companion object {
        val busy = java.util.concurrent.atomic.AtomicBoolean(false)
    }
}
