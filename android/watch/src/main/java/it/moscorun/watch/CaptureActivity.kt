package it.moscorun.watch

import android.Manifest
import android.app.Activity
import android.app.AlertDialog
import android.content.pm.PackageManager
import android.os.Bundle
import android.text.InputType
import android.widget.*
import com.samsung.android.service.health.tracking.data.HealthTrackerType
import com.samsung.android.service.health.tracking.data.TrackerUserProfile
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
    private var selectedProfile: TrackerUserProfile? = null

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
            } })
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
        if (type == HealthTrackerType.MF_BIA_ON_DEMAND || type == HealthTrackerType.BIA_ON_DEMAND) {
            AlertDialog.Builder(this).setTitle("Impedenza bioelettrica")
                .setMessage("Non utilizzare con dispositivi medici impiantati o durante la gravidanza. Segui le istruzioni Samsung per il contatto degli elettrodi.")
                .setNegativeButton("Annulla", null).setPositiveButton("Continua") { _, _ -> profileDialog(type) }.show()
        } else authorize(type)
    }

    private fun profileDialog(type: HealthTrackerType) {
        val form = LinearLayout(this).apply { orientation = LinearLayout.VERTICAL }
        fun number(hintText: String, decimal: Boolean) = EditText(this).apply {
            hint = hintText
            inputType = InputType.TYPE_CLASS_NUMBER or (if (decimal) InputType.TYPE_NUMBER_FLAG_DECIMAL else 0)
            form.addView(this)
        }
        val height = number("Altezza (cm)", true)
        val weight = number("Peso (kg)", true)
        val age = number("Età (anni)", false)
        val gender = Spinner(this).apply {
            adapter = ArrayAdapter(this@CaptureActivity, android.R.layout.simple_spinner_dropdown_item,
                listOf("Seleziona sesso richiesto dall'SDK", "Femminile", "Maschile"))
            form.addView(this)
        }
        val dialog = AlertDialog.Builder(this).setTitle("Profilo per BIA")
            .setMessage("Dati richiesti dal calcolo Samsung. Usati solo per questa misura, non salvati.")
            .setView(form).setNegativeButton("Annulla", null).setPositiveButton("Avvia", null).create()
        dialog.setOnShowListener {
            dialog.getButton(AlertDialog.BUTTON_POSITIVE).setOnClickListener {
                val h = height.text.toString().replace(',', '.').toFloatOrNull()
                val w = weight.text.toString().replace(',', '.').toFloatOrNull()
                val a = age.text.toString().toIntOrNull()
                val g = gender.selectedItemPosition - 1
                if (h == null || h !in 20f..300f || w == null || w !in 2f..500f ||
                    a == null || a !in 18..200 || g !in 0..1) {
                    status.text = "Profilo BIA non valido; completa tutti i campi"
                    Toast.makeText(this, "Controlla i dati del profilo", Toast.LENGTH_SHORT).show()
                    return@setOnClickListener
                }
                val profile = TrackerUserProfile.Builder().setHeight(h).setWeight(w).setAge(a).setGender(g).build()
                dialog.dismiss()
                authorize(type, profile)
            }
        }
        dialog.show()
    }

    private fun authorize(type: HealthTrackerType, profile: TrackerUserProfile? = null) {
        val permission = if (type == HealthTrackerType.ACCELEROMETER_CONTINUOUS)
            Manifest.permission.ACTIVITY_RECOGNITION else Manifest.permission.BODY_SENSORS
        if (checkSelfPermission(permission) == PackageManager.PERMISSION_GRANTED) start(type, profile)
        else { selected = type; selectedProfile = profile; requestPermissions(arrayOf(permission), 1) }
    }

    override fun onRequestPermissionsResult(requestCode: Int, permissions: Array<out String>, grantResults: IntArray) {
        super.onRequestPermissionsResult(requestCode, permissions, grantResults)
        if (requestCode == 1) {
            val type = selected
            val profile = selectedProfile
            selected = null
            selectedProfile = null
            if (type != null && grantResults.firstOrNull() == PackageManager.PERMISSION_GRANTED) start(type, profile)
            else status.text = "Permesso non concesso"
        }
    }

    private fun start(type: HealthTrackerType, profile: TrackerUserProfile?) {
        val target = connection ?: return
        // Capture the account at session start, never route a late callback to new credentials.
        runCatching { capture.start(type, profile) { payloads ->
            queue.append(target, payloads)
            drain()
        } }.onFailure { status.text = "Tracker non disponibile o SDK non autorizzato" }
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
