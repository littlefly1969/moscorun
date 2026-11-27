package it.moscorun.watch

import android.Manifest
import android.app.Activity
import android.app.AlertDialog
import android.content.pm.PackageManager
import android.os.Bundle
import android.text.InputType
import android.widget.*
import com.samsung.android.service.health.tracking.data.HealthTrackerType
import java.util.concurrent.Executors
import java.util.concurrent.atomic.AtomicBoolean

/** Foreground capture with explicit consent and automatic HTTPS upload; no export files or background tracking. */
class CaptureActivity : Activity() {
    private lateinit var vault: WatchVault
    private lateinit var queue: WatchQueue
    private lateinit var capture: SamsungCapture
    private lateinit var status: TextView
    private lateinit var syncStatus: TextView
    private lateinit var buttons: LinearLayout
    private var connection: WatchConnection? = null
    private val worker = Executors.newSingleThreadExecutor()
    private val draining = AtomicBoolean(false)
    private var selected: HealthTrackerType? = null

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        vault = WatchVault(this)
        queue = WatchQueue(this)
        connection = vault.load()
        val layout = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            setPadding(16, 20, 16, 20)
        }
        setContentView(ScrollView(this).apply { addView(layout) })
        fun button(text: String, action: () -> Unit) = Button(this).apply { this.text = text; setOnClickListener { action() } }
        status = TextView(this).apply { text = "Connessione Samsung SDK…" }
        syncStatus = TextView(this)
        layout.addView(TextView(this).apply { text = "MoscoRun Watch · ricerca e benessere, non dispositivo medico" })
        layout.addView(status)
        layout.addView(syncStatus)
        layout.addView(button("Configura server") { configure() })
        layout.addView(button("Sincronizza coda") { drain() })
        layout.addView(button("Termina misura") { capture.stop(); status.text = "Misura terminata"; drain() })
        buttons = LinearLayout(this).apply { orientation = LinearLayout.VERTICAL }
        layout.addView(buttons)
        capture = SamsungCapture(this, { msg -> runOnUiThread { status.text = msg } },
            { available -> runOnUiThread {
                buttons.removeAllViews()
                if (available.isEmpty()) buttons.addView(TextView(this).apply { text = "Nessun tracker integrato disponibile" })
                available.forEach { type -> buttons.addView(button(type.name) { request(type) }) }
            } }, { payloads ->
                val target = checkNotNull(connection) { "Server non configurato" }
                queue.append(target, payloads)
                drain()
            })
        capture.connect()
        drain()
    }

    private fun configure() {
        capture.stop()
        val input = LinearLayout(this).apply { orientation = LinearLayout.VERTICAL }
        val url = EditText(this).apply { hint = "https://server"; setText(connection?.base.orEmpty()); inputType = InputType.TYPE_CLASS_TEXT or InputType.TYPE_TEXT_VARIATION_URI }
        val token = EditText(this).apply { hint = "Token dispositivo"; inputType = InputType.TYPE_CLASS_TEXT or InputType.TYPE_TEXT_VARIATION_PASSWORD }
        input.addView(url); input.addView(token)
        AlertDialog.Builder(this).setTitle("Server MoscoRun").setMessage("Crea un token dispositivo separato per l'orologio. La coda di un altro token non sarà inviata.")
            .setView(input).setNegativeButton("Annulla", null).setPositiveButton("Salva") { _, _ ->
                runCatching {
                    val next = WatchConnection.checked(url.text.toString(), token.text.toString())
                    vault.save(next)
                    connection = next
                    token.text.clear()
                    drain()
                }.onFailure { status.text = "Configurazione non valida o Keystore non disponibile" }
            }.show()
    }

    private fun request(type: HealthTrackerType) {
        if (connection == null) { status.text = "Configura il server prima di acquisire"; return }
        if (type == HealthTrackerType.MF_BIA_ON_DEMAND) {
            AlertDialog.Builder(this).setTitle("Impedenza bioelettrica")
                .setMessage("Non utilizzare con dispositivi medici impiantati o durante la gravidanza. Segui le istruzioni Samsung per il contatto degli elettrodi.")
                .setNegativeButton("Annulla", null).setPositiveButton("Continua") { _, _ -> authorize(type) }.show()
        } else authorize(type)
    }

    private fun authorize(type: HealthTrackerType) {
        val permission = if (type == HealthTrackerType.ACCELEROMETER_CONTINUOUS)
            Manifest.permission.ACTIVITY_RECOGNITION else Manifest.permission.BODY_SENSORS
        if (checkSelfPermission(permission) == PackageManager.PERMISSION_GRANTED) start(type)
        else { selected = type; requestPermissions(arrayOf(permission), 1) }
    }

    override fun onRequestPermissionsResult(requestCode: Int, permissions: Array<out String>, grantResults: IntArray) {
        super.onRequestPermissionsResult(requestCode, permissions, grantResults)
        if (requestCode == 1) {
            val type = selected
            selected = null
            if (type != null && grantResults.firstOrNull() == PackageManager.PERMISSION_GRANTED) start(type)
            else status.text = "Permesso non concesso"
        }
    }

    private fun start(type: HealthTrackerType) {
        runCatching { capture.start(type) }.onFailure { status.text = "Tracker non disponibile o SDK non autorizzato" }
    }

    private fun drain() {
        val target = connection ?: return
        if (!draining.compareAndSet(false, true)) return
        worker.execute {
            try {
                val result = queue.drain(target)
                runOnUiThread { syncStatus.text = result }
            } finally { draining.set(false) }
        }
    }

    override fun onStop() {
        capture.stop()
        drain()
        super.onStop()
    }
    override fun onDestroy() {
        capture.close()
        worker.shutdown()
        super.onDestroy()
    }
}
