package it.moscorun.app.ui

import android.content.ClipData
import android.content.ClipboardManager
import android.view.View
import android.widget.TextView
import android.widget.Toast
import it.moscorun.app.server.WorkoutFormat
import it.moscorun.app.settings.TokenState
import it.moscorun.app.source.SourceStatus
import it.moscorun.app.sync.ItemState
import it.moscorun.app.sync.RunOutcome
import it.moscorun.app.sync.SyncStatus
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.delay
import kotlinx.coroutines.isActive
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext

/** Words for the outcomes and states of the synchronisation, shared by the screens. */
internal object SyncWords {
    fun outcome(outcome: RunOutcome?): String = when (outcome) {
        null -> "Mai eseguita"
        RunOutcome.COMPLETED -> "Completata"
        RunOutcome.MORE_WORK -> "In corso a tappe: lo storico continua da solo"
        RunOutcome.RETRY_LATER -> "Rimandata: rete o server non disponibili"
        RunOutcome.AUTH_REQUIRED -> "Ferma: il token va sostituito"
        RunOutcome.BUSY -> "Già in corso"
        RunOutcome.NOT_CONFIGURED -> "Server non configurato"
        RunOutcome.SOURCE_BLOCKED -> "Ferma: la sorgente richiede il tuo intervento"
    }

    fun state(state: ItemState): String = when (state) {
        ItemState.SYNCED -> "Sincronizzati"
        ItemState.INVALID -> "Rifiutati dal server"
        ItemState.CONFLICT -> "In conflitto di revisione"
        ItemState.TOO_LARGE -> "Troppo grandi"
        ItemState.DUPLICATE_SUSPECT -> "Possibili doppioni in attesa"
        ItemState.FAILED -> "Da ritentare"
    }

    fun status(status: SyncStatus): String = buildString {
        append("Ultimo tentativo: ").append(status.lastAttemptAt?.let(WorkoutFormat::dateTime) ?: "mai")
        append("\nUltimo successo: ").append(status.lastSuccessAt?.let(WorkoutFormat::dateTime) ?: "mai")
        append("\nEsito: ").append(outcome(status.outcome))
        status.message?.takeIf { it.isNotBlank() }?.let { append("\n").append(it) }
    }
}

/** State of the synchronisation at a glance, and the button to run it now. */
class HomeScreen : Screen() {
    private var crash: String? = null
    private var crashTaken = false
    private var notice: String? = null

    override fun build(): View {
        if (!crashTaken) { crash = host.takeCrash(); crashTaken = true }
        val configured = host.settings.configured
        val scope = host.sync.scope()
        lateinit var live: TextView
        lateinit var summary: TextView
        lateinit var counts: TextView
        val view = page {
            heading("MoscoRun", "I tuoi allenamenti")
            crash?.let { report -> crashCard(report) }
            notice?.let { label(it, 14f, color = Palette.warning) }
            if (!configured) {
                card {
                    label("Server non configurato", 17f, bold = true)
                    label("Inserisci una sola volta l'indirizzo del tuo server MoscoRun e il token del telefono: restano salvati, cifrati, anche dopo riavvii e aggiornamenti.", 13f, color = Palette.muted)
                    action("Configura il server") { host.openTab(Tab.SETTINGS) }
                }
                live = label(""); summary = label(""); counts = label("")
                listOf(live, summary, counts).forEach { it.visibility = View.GONE }
                return@page
            }
            if (host.settings.tokenState() is TokenState.Unreadable || host.prefs.authBlocked) card {
                label("Il token va inserito di nuovo", 17f, bold = true, color = Palette.warning)
                label(if (host.prefs.authBlocked) "Il server non accetta più il token di questo telefono: creane uno nuovo dal cruscotto (Dispositivi) e sostituiscilo."
                    else "Il token salvato non è più leggibile su questo telefono (può succedere dopo un ripristino). Inseriscilo di nuovo, una sola volta.", 13f)
                action("Sostituisci token") { host.openTab(Tab.SETTINGS) }
            }
            card {
                label("Sincronizzazione", 17f, bold = true)
                label("Sorgente: ${host.prefs.provider.label} · " +
                    if (host.prefs.autoSync) "automatica, circa ogni ${host.prefs.intervalHours} h" else "automatica spenta", 13f, color = Palette.muted)
                live = label("", 14f, color = Palette.green)
                summary = label("", 14f)
                action("Sincronizza ora") { syncNow() }
                if (!host.prefs.autoSync) label("La sincronizzazione automatica è spenta: si attiva dalle Impostazioni.", 12f, color = Palette.muted)
            }
            card {
                label("Sul server da questo telefono", 17f, bold = true)
                counts = label("", 14f)
                action("Diagnostica") { host.open(DiagnosticsScreen()) }
            }
            action("Vedi gli allenamenti") { host.openTab(Tab.WORKOUTS) }
        }
        if (configured && scope != null) visible.launch {
            // The run happens in the background worker: the screen only watches its durable state.
            while (isActive) {
                val (status, byState) = withContext(Dispatchers.IO) { host.store.status(scope) to host.store.counts(scope) }
                val progress = host.liveProgress
                live.text = when {
                    progress != null -> "In corso · ${progress.activity}\n${progress.report.describe()}"
                    host.sync.running -> "In corso…"
                    else -> ""
                }
                live.visibility = if (live.text.isEmpty()) View.GONE else View.VISIBLE
                summary.text = SyncWords.status(status)
                counts.text = if (byState.isEmpty()) "Ancora nessun allenamento inviato."
                    else ItemState.entries.filter { (byState[it] ?: 0) > 0 }.joinToString("\n") { "${SyncWords.state(it)}: ${byState[it]}" }
                delay(1000)
            }
        }
        return view
    }

    private fun syncNow() {
        visible.launch {
            val provider = host.prefs.provider
            when (val status = host.sourceStatus(provider)) {
                SourceStatus.Ready -> {
                    notice = null
                    host.syncNow()
                    Toast.makeText(context, "Sincronizzazione avviata", Toast.LENGTH_SHORT).show()
                }
                is SourceStatus.NeedsPermission -> host.requestSourcePermissions(provider) {
                    visible.launch {
                        if (host.sourceStatus(provider) is SourceStatus.Ready) { notice = null; host.syncNow() } else notice = status.message
                        host.refresh()
                    }
                }
                is SourceStatus.Unavailable -> { notice = status.message; host.refresh() }
            }
        }
    }

    private fun android.widget.LinearLayout.crashCard(report: String) = card {
        label("L'app si è chiusa per un errore", 17f, bold = true)
        label("Copia il dettaglio e invialo a chi ti assiste: contiene solo l'errore tecnico, nessun dato di salute.", 12f, color = Palette.muted)
        label(report.lineSequence().take(6).joinToString("\n"), 11f)
        action("Copia dettaglio errore") {
            context.getSystemService(ClipboardManager::class.java).setPrimaryClip(ClipData.newPlainText("Errore MoscoRun", report))
            Toast.makeText(context, "Dettaglio copiato", Toast.LENGTH_SHORT).show()
        }
        action("Chiudi") { crash = null; host.refresh() }
    }
}
