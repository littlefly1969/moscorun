package it.moscorun.app.ui

import android.app.AlertDialog
import android.text.InputType
import android.view.View
import android.widget.AdapterView
import android.widget.ArrayAdapter
import android.widget.Button
import android.widget.CheckBox
import android.widget.EditText
import android.widget.LinearLayout
import android.widget.RadioButton
import android.widget.Spinner
import android.widget.TextView
import it.moscorun.app.model.Provider
import it.moscorun.app.settings.AppPreferences
import it.moscorun.app.settings.ServerSetup
import it.moscorun.app.settings.TokenState
import it.moscorun.app.source.SourceStatus
import it.moscorun.app.sync.SyncPlan
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext

/** Server (entered once), source, schedule and history depth. */
class SettingsScreen(private val setup: (AppHost) -> ServerSetup) : Screen() {
    private var busy = false
    private var serverMessage: String? = null
    private var draftUrl = ""
    private var recheckSource: (() -> Unit)? = null

    override fun resumed() { if (attached) recheckSource?.invoke() }

    override fun build(): View = page {
        heading("MoscoRun", "Impostazioni")
        serverCard()
        sourceCard()
        scheduleCard()
        card {
            label("Informazioni", 17f, bold = true)
            action("Diagnostica") { host.open(DiagnosticsScreen()) }
            action("Dati e riservatezza") { host.openPrivacy() }
        }
    }

    private fun LinearLayout.serverCard() = card {
        val url = host.settings.serverUrl
        if (url == null || !host.settings.configured) {
            label("Server", 17f, bold = true)
            label("Si inseriscono una sola volta. Il token si crea nel cruscotto web, sezione Dispositivi, tipo \"Telefono Android\".", 13f, color = Palette.muted)
            val address = input("https://indirizzo-del-tuo-server", InputType.TYPE_CLASS_TEXT or InputType.TYPE_TEXT_VARIATION_URI).apply { setText(draftUrl) }
            val token = input("Token del telefono (mr1_…)", InputType.TYPE_CLASS_TEXT or InputType.TYPE_TEXT_VARIATION_PASSWORD)
            serverMessage?.let { label(it, 13f, color = Palette.warning) }
            action(if (busy) "Verifica in corso…" else "Salva e verifica", enabled = !busy) {
                draftUrl = address.text.toString()
                val secret = token.text.toString()
                server { it.connect(draftUrl, secret) }
            }
            return@card
        }
        label("Server configurato", 17f, bold = true)
        label(url, 14f)
        label("Token: ${host.settings.maskedToken()}" + (host.prefs.deviceName?.let { " · dispositivo \"$it\"" } ?: ""), 13f, color = Palette.muted)
        if (host.settings.tokenState() is TokenState.Unreadable)
            label("Il token salvato non è più leggibile su questo telefono: sostituiscilo, una sola volta. L'indirizzo e lo stato della sincronizzazione restano.", 13f, color = Palette.warning)
        else if (host.prefs.authBlocked)
            label("Il server non accetta più questo token: sostituiscilo. La sincronizzazione automatica riprende subito dopo.", 13f, color = Palette.warning)
        serverMessage?.let { label(it, 13f, color = Palette.green) }
        action(if (busy) "Verifica in corso…" else "Verifica connessione", enabled = !busy) { server { it.verify() } }
        action("Sostituisci token", enabled = !busy) { askToken() }
        action("Scollega", enabled = !busy) {
            AlertDialog.Builder(context)
                .setTitle("Scollegare il server?")
                .setMessage("Indirizzo, token e registro della sincronizzazione vengono cancellati da questo telefono. Gli allenamenti già sul server restano dove sono. Per sincronizzare di nuovo dovrai reinserire indirizzo e token.")
                .setPositiveButton("Scollega") { _, _ -> host.disconnect(); serverMessage = null; draftUrl = ""; host.refresh() }
                .setNegativeButton("Annulla", null).show()
        }
    }

    private fun askToken() {
        val field = EditText(context).apply {
            hint = "Nuovo token (mr1_…)"
            inputType = InputType.TYPE_CLASS_TEXT or InputType.TYPE_TEXT_VARIATION_PASSWORD
        }
        AlertDialog.Builder(context)
            .setTitle("Sostituisci token")
            .setMessage("Il nuovo token viene verificato sul server prima di sostituire quello salvato.")
            .setView(field)
            .setPositiveButton("Verifica e salva") { _, _ ->
                val secret = field.text.toString()
                server { it.replaceToken(secret) }
            }
            .setNegativeButton("Annulla", null).show()
    }

    /** Runs a server operation off the main thread, then applies the schedule: a new token restarts the background work. */
    private fun server(operation: (ServerSetup) -> ServerSetup.Outcome) {
        if (busy) return
        busy = true
        serverMessage = null
        host.refresh()
        host.scope.launch {
            val outcome = try { withContext(Dispatchers.IO) { operation(setup(host)) } } finally { busy = false }
            serverMessage = outcome.message
            host.reschedule()
            if (attached) host.refresh()
        }
    }

    private fun LinearLayout.sourceCard() = card {
        label("Sorgente degli allenamenti", 17f, bold = true)
        label("Da dove l'app legge sul telefono. Si può cambiare in ogni momento: server e token non vengono richiesti di nuovo.", 13f, color = Palette.muted)
        val current = host.prefs.provider
        Provider.entries.forEach { provider ->
            addView(RadioButton(context).apply {
                text = if (provider == Provider.DEFAULT) "${provider.label} (consigliata: dati più completi)" else provider.label
                isChecked = provider == current
                setOnClickListener { if (provider != current) confirmProvider(provider) else isChecked = true }
            })
        }
        val status = label("Verifica della sorgente…", 13f, color = Palette.muted)
        val grant = action("Concedi i permessi") {
            host.requestSourcePermissions(current) { if (attached) host.refresh() }
        }.apply { visibility = View.GONE }
        val hints = label("", 12f, color = Palette.muted).apply { visibility = View.GONE }
        recheckSource = { visible.launch { showSource(current, status, grant, hints) } }
        recheckSource?.invoke()
    }

    private suspend fun showSource(provider: Provider, status: TextView, grant: Button, hints: TextView) {
        val state = host.sourceStatus(provider)
        status.text = when (state) {
            SourceStatus.Ready -> "${provider.label}: pronta."
            else -> state.message
        }
        status.setTextColor(if (state is SourceStatus.Ready) Palette.green else Palette.warning)
        val missing = host.sourceHints(provider)
        grant.visibility = if (state is SourceStatus.NeedsPermission || missing.isNotEmpty()) View.VISIBLE else View.GONE
        if (state is SourceStatus.Ready && missing.isNotEmpty()) grant.text = "Rivedi i permessi"
        hints.text = missing.joinToString("\n") { "• $it" }
        hints.visibility = if (missing.isEmpty()) View.GONE else View.VISIBLE
    }

    private fun confirmProvider(provider: Provider) {
        AlertDialog.Builder(context)
            .setTitle("Passare a ${provider.label}?")
            .setMessage("Gli allenamenti già sul server restano. Quelli che la nuova sorgente ripropone e che coincidono per orario con uno già presente non vengono inviati né cancellati: restano in attesa in Diagnostica come possibili doppioni, e decidi tu.")
            .setPositiveButton("Cambia") { _, _ -> host.prefs.provider = provider; host.refresh() }
            .setNegativeButton("Annulla") { _, _ -> host.refresh() }
            .setOnCancelListener { host.refresh() }.show()
    }

    private fun LinearLayout.scheduleCard() = card {
        label("Sincronizzazione", 17f, bold = true)
        addView(CheckBox(context).apply {
            text = "Sincronizza automaticamente in background"
            isChecked = host.prefs.autoSync
            setOnCheckedChangeListener { _, checked -> host.prefs.autoSync = checked; host.reschedule() }
        })
        label("Android sceglie il momento in base a batteria e rete: l'intervallo è indicativo. Nessuna notifica fissa, nessun servizio sempre attivo.", 12f, color = Palette.muted)
        choice("Ogni", AppPreferences.INTERVAL_CHOICES.map { it to if (it == 1) "1 ora" else "$it ore" }, host.prefs.intervalHours) {
            host.prefs.intervalHours = it; host.reschedule()
        }
        addView(CheckBox(context).apply {
            text = "Solo con Wi-Fi"
            isChecked = host.prefs.unmeteredOnly
            setOnCheckedChangeListener { _, checked -> host.prefs.unmeteredOnly = checked; host.reschedule() }
        })
        val history = (1..SyncPlan.MAX_HISTORY_YEARS).map { it to if (it == 1) "Ultimo anno" else "Ultimi $it anni" } +
            (SyncPlan.ALL_HISTORY to "Tutto lo storico disponibile")
        choice("Storico da allineare", history, host.prefs.historyYears) { host.prefs.historyYears = it }
        label("Lo storico viene letto a blocchi, dai più recenti ai più vecchi, riprendendo da dove era arrivato. Allungarlo aggiunge solo il periodo mancante.", 12f, color = Palette.muted)
        action("Rileggi tutto lo storico") {
            AlertDialog.Builder(context)
                .setTitle("Rileggere lo storico?")
                .setMessage("L'app rilegge tutto il periodo scelto dalla sorgente. Gli allenamenti invariati non vengono inviati di nuovo.")
                .setPositiveButton("Rileggi") { _, _ ->
                    host.scope.launch {
                        withContext(Dispatchers.IO) { host.sync.scope()?.let(host.store::resetCheckpoints) }
                        host.syncNow()
                    }
                }
                .setNegativeButton("Annulla", null).show()
        }
    }

    private fun LinearLayout.input(hintText: String, type: Int): EditText = EditText(context).apply {
        hint = hintText
        inputType = type
        setSingleLine()
        this@input.addView(this)
    }

    private fun LinearLayout.choice(title: String, options: List<Pair<Int, String>>, selected: Int, onChoice: (Int) -> Unit) {
        label(title, 13f, color = Palette.muted)
        addView(Spinner(context).apply {
            adapter = ArrayAdapter(context, android.R.layout.simple_spinner_dropdown_item, options.map { it.second })
            val initial = options.indexOfFirst { it.first == selected }.coerceAtLeast(0)
            setSelection(initial)
            onItemSelectedListener = object : AdapterView.OnItemSelectedListener {
                private var current = initial
                override fun onItemSelected(parent: AdapterView<*>?, view: View?, position: Int, id: Long) {
                    // The spinner reports its initial selection too: only a real change is a choice.
                    if (position != current) { current = position; onChoice(options[position].first) }
                }
                override fun onNothingSelected(parent: AdapterView<*>?) {}
            }
        })
    }
}
