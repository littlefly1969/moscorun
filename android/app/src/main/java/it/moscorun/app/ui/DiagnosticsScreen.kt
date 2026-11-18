package it.moscorun.app.ui

import android.view.View
import android.widget.LinearLayout
import it.moscorun.app.model.Provider
import it.moscorun.app.server.WorkoutFormat
import it.moscorun.app.sync.Checkpoint
import it.moscorun.app.sync.ItemState
import it.moscorun.app.sync.LedgerEntry
import it.moscorun.app.sync.SyncStatus
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext

/**
 * What the synchronisation did and what is waiting for a decision. It shows identities, times and outcomes only:
 * no health measurement is kept in the ledger, so none can appear here.
 */
class DiagnosticsScreen : Screen() {
    private class Snapshot(val status: SyncStatus, val counts: Map<ItemState, Int>,
        val checkpoints: Map<Provider, Checkpoint?>, val problems: List<LedgerEntry>)

    private var snapshot: Snapshot? = null

    override fun build(): View {
        val scope = host.sync.scope()
        val view = page {
            action("← Indietro") { host.back() }
            heading("MoscoRun", "Diagnostica")
            if (scope == null) { label("Server non configurato: non c'è ancora nulla da mostrare.", 14f); return@page }
            val data = snapshot
            if (data == null) { label("Lettura dello stato…", 13f, color = Palette.muted); return@page }
            card {
                label("Ultima sincronizzazione", 17f, bold = true)
                label(SyncWords.status(data.status), 14f)
            }
            card {
                label("Registro", 17f, bold = true)
                if (data.counts.isEmpty()) label("Vuoto.", 14f)
                ItemState.entries.forEach { state -> data.counts[state]?.takeIf { it > 0 }?.let { label("${SyncWords.state(state)}: $it", 14f) } }
            }
            card {
                label("Storico letto", 17f, bold = true)
                Provider.entries.forEach { provider -> label("${provider.label}: ${coverage(data.checkpoints[provider])}", 14f) }
            }
            card {
                label("Da rivedere", 17f, bold = true)
                if (data.problems.isEmpty()) label("Niente in sospeso.", 14f)
                data.problems.forEach { problem(scope, it) }
                if (data.problems.size == PROBLEM_LIMIT) label("Mostrati i $PROBLEM_LIMIT più recenti.", 12f, color = Palette.muted)
            }
            if (host.oldAppInstalled) card {
                label("Vecchia app \"MoscoRun HC\" ancora installata", 17f, bold = true)
                label(if (data.status.lastSuccessAt != null)
                    "Non serve più: quest'app legge anche Health Connect. La vecchia app non conservava dati da trasferire. Puoi disinstallarla a mano; MoscoRun non la rimuove da sola."
                    else "Tienila finché la prima sincronizzazione di quest'app non è riuscita; poi potrai disinstallarla a mano.", 13f)
                if (data.status.lastSuccessAt != null) action("Apri la scheda della vecchia app") { host.openOldAppSettings() }
            }
        }
        if (scope != null && snapshot == null) visible.launch {
            snapshot = withContext(Dispatchers.IO) {
                Snapshot(host.store.status(scope), host.store.counts(scope),
                    Provider.entries.associateWith { host.store.checkpoint(scope, it) }, host.store.problems(scope, PROBLEM_LIMIT))
            }
            host.refresh()
        }
        return view
    }

    private fun coverage(checkpoint: Checkpoint?): String {
        val from = checkpoint?.coveredFrom
        val until = checkpoint?.coveredUntil
        if (checkpoint == null || from == null || until == null) return "non ancora letto"
        return "dal ${WorkoutFormat.day(from)} al ${WorkoutFormat.day(until)}" + when {
            checkpoint.exhausted -> " · storico completato"
            checkpoint.blockStart != null -> " · lettura in corso (blocchi di ${checkpoint.blockDays} giorni)"
            else -> ""
        }
    }

    private fun LinearLayout.problem(scope: String, entry: LedgerEntry) {
        val time = entry.startedAt?.let(WorkoutFormat::dateTime) ?: "data non nota"
        label("${SyncWords.state(entry.state)} · ${entry.provider.label} · $time", 14f, bold = true)
        entry.detail?.let { label(it, 12f, color = Palette.muted) }
        when {
            entry.forced -> label("Verrà inviato alla prossima sincronizzazione.", 12f, color = Palette.green)
            entry.state == ItemState.DUPLICATE_SUSPECT -> action("Non è un doppione: invia comunque") { force(scope, entry) }
            entry.state != ItemState.FAILED -> action("Riprova l'invio") { force(scope, entry) }
        }
    }

    private fun force(scope: String, entry: LedgerEntry) {
        host.scope.launch {
            withContext(Dispatchers.IO) { host.store.force(scope, entry.provider, entry.sourceId) }
            snapshot = null
            if (attached) host.refresh()
        }
    }

    private companion object {
        const val PROBLEM_LIMIT = 50
    }
}
