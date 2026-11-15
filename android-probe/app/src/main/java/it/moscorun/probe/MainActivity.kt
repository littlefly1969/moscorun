package it.moscorun.probe

import android.app.AlertDialog
import android.content.Intent
import android.graphics.Color
import android.graphics.Typeface
import android.graphics.drawable.GradientDrawable
import android.net.Uri
import android.os.Bundle
import android.view.View
import android.view.WindowManager
import android.widget.*
import androidx.activity.ComponentActivity
import androidx.activity.result.contract.ActivityResultContracts
import androidx.health.connect.client.HealthConnectClient
import androidx.health.connect.client.PermissionController
import androidx.health.connect.client.contracts.ExerciseRouteRequestContract
import androidx.lifecycle.lifecycleScope
import kotlinx.coroutines.*
import java.time.Instant
import java.time.temporal.ChronoUnit

class MainActivity:ComponentActivity() {
    private lateinit var hc:HealthConnectProbe
    private lateinit var samsung:SamsungGateway
    private val settings by lazy { SecureSettings(this) }
    private lateinit var root:LinearLayout
    private var provider="Health Connect"
    private var days=7
    private var samsungOnly=true
    private var busy=false
    private var message="Scegli un collegamento, concedi i permessi e avvia la verifica."
    private val reports=mutableMapOf<String,ProbeReport>()
    private var selected:Workout?=null
    private var remote:List<RemoteWorkout>?=null
    private var remoteDetail:Pair<RemoteWorkout,RemoteRoute?>?=null
    private var pendingRoute:Workout?=null
    private var pendingExport:String?=null
    private var task:Job?=null
    private val green=Color.rgb(23,106,82)
    private val permissions=registerForActivityResult(PermissionController.createRequestPermissionResultContract()) { granted ->
        message="Permessi Health Connect concessi: ${granted.intersect(hc.permissions).size}/${hc.permissions.size}. Avvia la verifica per leggere i dati."
        render()
    }
    private val routes=registerForActivityResult(ExerciseRouteRequestContract()) { route ->
        val workout=pendingRoute;pendingRoute=null
        if(workout!=null) {
            if(route==null) { workout.routeState="consenso_non_concesso_o_dato_assente" }
            else { workout.route=hc.routePoints(route);workout.rawRoute=Json.tree(route);workout.routeState="disponibile" }
            selected=workout
        }
        render()
    }
    private val export=registerForActivityResult(ActivityResultContracts.CreateDocument("application/json")) { uri ->
        val payload=pendingExport;pendingExport=null
        if(uri!=null && payload!=null) runTask {
            withContext(Dispatchers.IO) {
                contentResolver.openOutputStream(uri)?.bufferedWriter()?.use { it.write(payload) }
                    ?: error("Impossibile aprire il file scelto.")
            }
            message="Rapporto JSON salvato nella destinazione scelta."
        }
    }
    override fun onCreate(state:Bundle?) {
        super.onCreate(state)
        window.addFlags(WindowManager.LayoutParams.FLAG_SECURE)
        hc=HealthConnectProbe(this);samsung=samsungGateway(this)
        render()
    }
    private fun dp(v:Int)=(v*resources.displayMetrics.density).toInt()
    private fun text(value:String,size:Float=15f,bold:Boolean=false,parent:LinearLayout=root):TextView {
        return TextView(this).apply {
            text=value;textSize=size;setTextColor(Color.rgb(24,48,41));setPadding(0,dp(5),0,dp(5))
            if(bold) setTypeface(typeface,Typeface.BOLD)
            parent.addView(this)
        }
    }
    private fun button(label:String,parent:LinearLayout=root,enabled:Boolean=!busy,action:()->Unit) {
        parent.addView(Button(this).apply { text=label;isAllCaps=false;isEnabled=enabled;setOnClickListener { action() } })
    }
    private fun card():LinearLayout = LinearLayout(this).apply {
        orientation=LinearLayout.VERTICAL;setPadding(dp(14),dp(12),dp(14),dp(12))
        background=GradientDrawable().apply { setColor(Color.WHITE);cornerRadius=dp(16).toFloat() }
        root.addView(this,LinearLayout.LayoutParams(-1,-2).apply { topMargin=dp(10) })
    }
    private fun runTask(block:suspend ()->Unit) {
        if(busy) return
        task=lifecycleScope.launch {
            busy=true;render()
            try { block() } catch(e:CancellationException) { throw e }
            catch(e:Exception) { message="Operazione non riuscita (${e.javaClass.simpleName}): ${e.message ?: "nessun dettaglio"}" }
            finally { busy=false;render() }
        }
    }
    private fun requireHealthConnect():Boolean {
        return when(hc.sdkStatus()) {
            HealthConnectClient.SDK_AVAILABLE -> true
            HealthConnectClient.SDK_UNAVAILABLE_PROVIDER_UPDATE_REQUIRED -> {
                message="Health Connect deve essere installato o aggiornato. Usa il pulsante Apri Health Connect.";render();false
            }
            else -> { message="Health Connect non è disponibile su questo dispositivo o profilo.";render();false }
        }
    }
    private fun render() {
        val scroll=ScrollView(this)
        root=LinearLayout(this).apply {
            orientation=LinearLayout.VERTICAL;setPadding(dp(18),dp(20),dp(18),dp(30));setBackgroundColor(Color.rgb(243,246,244))
        }
        scroll.addView(root);setContentView(scroll)
        // Android 15+ edge-to-edge: leave room for system bars and display cutouts.
        root.setOnApplyWindowInsetsListener { v,insets ->
            v.setPadding(dp(18)+insets.systemWindowInsetLeft,dp(16)+insets.systemWindowInsetTop,
                dp(18)+insets.systemWindowInsetRight,dp(24)+insets.systemWindowInsetBottom);insets
        }
        root.requestApplyInsets()
        text("MOSCORUN",12f,true).setTextColor(green)
        text("Esplora i tuoi dati",28f,true)
        text(if(serverSyncAvailable()) "Lettura locale · invio al tuo server solo su comando" else "Prova locale · sola lettura · nessun invio al server",13f)
        text(message,14f)
        if(busy) root.addView(ProgressBar(this))
        val detail=selected
        if(detail!=null) { renderDetail(detail);return }
        remoteDetail?.let { (w,route) -> renderRemote(w,route);return }
        if(canOpenGoogleRouteMap()) button("Prova Google Maps") { openGoogleMapPreview(this) }
        val tabs=LinearLayout(this).apply { orientation=LinearLayout.HORIZONTAL;root.addView(this) }
        button(if(provider=="Health Connect") "✓ Health Connect" else "Health Connect",tabs) { provider="Health Connect";message="Health Connect selezionato.";render() }
        button(if(provider=="Samsung") "✓ Samsung" else "Samsung diretto",tabs) { provider="Samsung";message=samsung.setupMessage;render() }
        val range=Spinner(this)
        range.adapter=ArrayAdapter(this,android.R.layout.simple_spinner_dropdown_item,listOf("Ultimi 7 giorni","Ultimi 30 giorni","Ultimi 90 giorni"))
        range.setSelection(when(days) { 7 -> 0; 30 -> 1; else -> 2 })
        range.isEnabled=!busy
        range.onItemSelectedListener=object:AdapterView.OnItemSelectedListener {
            override fun onItemSelected(p:AdapterView<*>?,v:View?,position:Int,id:Long) { days=when(position) { 0 -> 7; 1 -> 30; else -> 90 } }
            override fun onNothingSelected(p:AdapterView<*>?) {}
        }
        root.addView(range)
        if(provider=="Health Connect") root.addView(CheckBox(this).apply {
            text="Solo dati pubblicati da Samsung Health";isChecked=samsungOnly;isEnabled=!busy
            setOnCheckedChangeListener { _,value -> samsungOnly=value }
        })
        button("1 · Concedi permessi di lettura",enabled=!busy && (provider=="Health Connect" || samsung.available)) {
            if(provider=="Health Connect") { if(requireHealthConnect()) permissions.launch(hc.permissions) }
            else runTask { message=samsung.requestPermissions(this@MainActivity) }
        }
        button("2 · Leggi e verifica",enabled=!busy && (provider=="Health Connect" || samsung.available)) {
            if(provider=="Health Connect" && !requireHealthConnect()) return@button
            val chosen=provider;val onlySamsung=samsungOnly;val end=Instant.now();val start=end.minus(days.toLong(),ChronoUnit.DAYS)
            runTask {
                message="Lettura in corso…"
                val report=withContext(Dispatchers.IO) { if(chosen=="Health Connect") hc.scan(start,end,onlySamsung) else samsung.scan(start,end) }
                reports[chosen]=report
                message="Verifica completata: ${report.workouts.size} allenamenti elencati. Controlla gli stati delle categorie."
            }
        }
        val report=reports[provider]
        if(report!=null) {
            val summary=card()
            text("Disponibilità dei dati",20f,true,summary)
            text("${report.windowStart} → ${report.windowEnd}",11f,parent=summary)
            report.categories.forEach { c ->
                text("${c.name} · ${c.status}",15f,true,summary)
                text("${c.records} record · ${c.samples ?: "—"} elementi/campioni${c.message?.let { "\n$it" } ?: ""}",12f,parent=summary)
                if(c.sources.isNotEmpty()) text(c.sources.entries.joinToString("\n") { "${it.key}: ${it.value}" },11f,parent=summary)
            }
            text("Allenamenti",22f,true)
            if(report.workouts.isEmpty()) text("Nessuna sessione leggibile nell'intervallo. Controlla permessi, filtro e sincronizzazione di Samsung Health.")
            report.workouts.forEach { w ->
                val c=card();text(w.title,17f,true,c);text("${w.start}\n${w.source}",12f,parent=c)
                button("Esamina percorso e campioni",c) {
                    selected=w
                    if(provider=="Health Connect" && !w.detailLoaded) runTask { withContext(Dispatchers.IO) { hc.details(w) } }
                    else render()
                }
            }
            report.notes.forEach { text(it,12f) }
            button("Esporta rapporto JSON") { confirmExport(report) }
        }
        if(serverSyncAvailable()) renderServer()
        if(provider=="Health Connect") button("Apri Health Connect") {
            try {
                startActivity(Intent(HealthConnectClient.ACTION_HEALTH_CONNECT_SETTINGS))
            } catch(_:Exception) {
                startActivity(Intent(Intent.ACTION_VIEW,Uri.parse("https://play.google.com/store/apps/details?id=com.google.android.apps.healthdata")))
            }
        }
        button("Come preparare Samsung Health") {
            AlertDialog.Builder(this).setTitle("Prima della verifica").setMessage(
                "Apri Samsung Health sul telefono e attendi la sincronizzazione con il Watch.\n\nPer Health Connect: in Samsung Health → Impostazioni → Health Connect abilita la condivisione delle categorie che vuoi verificare. Poi torna qui e concedi i permessi a MoscoRun.\n\nPer Samsung diretto: serve la variante con SDK ufficiale e l'abilitazione prevista da Samsung per sviluppo o app partner. Il repository descrive i prerequisiti.\n\nLa sincronizzazione Watch → telefono può essere ritardata. Zero record non significa che il dato non esista sul Watch."
            ).setPositiveButton("Ho capito",null).show()
        }
        button("Informazioni sui dati") { startActivity(Intent(this,PrivacyActivity::class.java)) }
        button("Cancella risultati dalla memoria") { reports.clear();selected=null;remote=null;remoteDetail=null;message="Risultati rimossi dalla memoria dell'app. I dati originali non sono stati modificati.";render() }
    }
    private fun renderServer() {
        val c=card()
        text("Il tuo server MoscoRun",20f,true,c)
        text(settings.serverUrl?.let { "Collegato a $it" } ?: "Non configurato. Nel cruscotto web registra questo telefono in Dispositivi e copia il token mostrato.",13f,parent=c)
        button("Configura server",c) { configureServer() }
        val workouts=reports["Samsung"]?.workouts.orEmpty()
        button("Invia allenamenti al server",c,enabled=!busy && settings.configured && workouts.isNotEmpty()) { confirmUpload(workouts) }
        if(workouts.isEmpty()) text("Leggi prima gli allenamenti con Samsung diretto.",12f,parent=c)
        button("Allenamenti sul server",c,enabled=!busy && settings.configured) { loadRemote() }
        remote?.let { list ->
            if(list.isEmpty()) text("Nessun allenamento sul server negli ultimi 90 giorni.",12f,parent=c)
            list.take(50).forEach { w ->
                button("${WorkoutFormat.activity(w.activityType)} · ${WorkoutFormat.dateTime(w.startedAt)} · ${WorkoutFormat.distance(w.distanceM)}",c) { openRemote(w) }
            }
        }
        if(settings.configured) button("Scollega dal server",c) {
            settings.forget();PreferencesSyncLedger(this).clear();remote=null
            message="Server scollegato: indirizzo e token rimossi dal telefono.";render()
        }
    }
    private fun remoteServer():MoscoRunServer {
        val url=settings.serverUrl ?: error("Server non configurato.")
        return MoscoRunServer(url,settings.token() ?: error("Token assente: configurare di nuovo il server."))
    }
    private fun <T> unwrap(result:ReadResult<T>):T? = when(result) {
        is ReadResult.Ok -> result.value
        is ReadResult.Failed -> { message=result.message;null }
    }
    private fun loadRemote() = runTask {
        message="Lettura degli allenamenti dal server…";render()
        unwrap(withContext(Dispatchers.IO) { remoteServer().workouts() })?.let {
            remote=it;message="${it.size} allenamenti sul server."
        }
    }
    private fun openRemote(w:RemoteWorkout) = runTask {
        val route=if(w.trackPointCount<2) null else {
            message="Lettura del percorso dal server…";render()
            unwrap(withContext(Dispatchers.IO) { remoteServer().route(w.id) }) ?: return@runTask
        }
        remoteDetail=w to route;message="Allenamento letto dal tuo server."
    }
    private fun renderRemote(w:RemoteWorkout,route:RemoteRoute?) {
        button("← Torna agli allenamenti") { remoteDetail=null;render() }
        val title="${WorkoutFormat.activity(w.activityType)} · ${WorkoutFormat.dateTime(w.startedAt)}"
        text(title,22f,true)
        val c=card()
        text("Distanza: ${WorkoutFormat.distance(w.distanceM)}\nDurata: ${WorkoutFormat.duration(w.seconds)}" +
            (if(w.activeDurationSeconds!=null) " (senza pause)" else "") +
            "\nPasso medio: ${WorkoutFormat.pace(w.seconds,w.distanceM)}\nFC media: ${WorkoutFormat.bpm(w.averageHeartRate)} · max ${WorkoutFormat.bpm(w.maxHeartRate)}",15f,parent=c)
        text("Percorso",18f,true)
        if(route==null) { text("Allenamento senza tracciato GPS.",13f);return }
        root.addView(RouteView(this,route.points),LinearLayout.LayoutParams(-1,dp(240)))
        val hidden=route.hiddenStartPoints+route.hiddenEndPoints
        text("${route.points.size} punti mostrati su ${route.totalPoints} salvati. Verde: partenza; arancio: arrivo." +
            if(hidden>0) " $hidden punti vicino a partenza o arrivo sono nascosti dalle zone private del server." else "",11f)
        if(canOpenGoogleRouteMap()) button("Apri mappa Google") {
            AlertDialog.Builder(this).setTitle("Mappa della corsa")
                .setMessage("Per mostrare la cartografia, MoscoRun richiede a Google le immagini della mappa dell'area. Il tracciato resta sul telefono e viene solo disegnato sopra la mappa.")
                .setNegativeButton("Resta qui",null)
                .setPositiveButton("Apri mappa") { _,_ ->
                    openGoogleRouteMap(this,Workout(w.id,title,w.startedAt,w.endedAt,"server",com.google.gson.JsonNull.INSTANCE,
                        routeState="dal_server",route=route.points))
                }.show()
        }
    }
    private fun configureServer() {
        val form=LinearLayout(this).apply { orientation=LinearLayout.VERTICAL;setPadding(dp(20),dp(8),dp(20),0) }
        val url=EditText(this).apply { hint="https://moscorun.example.it";setText(settings.serverUrl.orEmpty());inputType=android.text.InputType.TYPE_TEXT_VARIATION_URI;form.addView(this) }
        val token=EditText(this).apply { hint="mr1_…";inputType=android.text.InputType.TYPE_CLASS_TEXT or android.text.InputType.TYPE_TEXT_VARIATION_PASSWORD;form.addView(this) }
        AlertDialog.Builder(this).setTitle("Server MoscoRun").setView(form)
            .setMessage("Indirizzo HTTPS del tuo server e token del dispositivo. Il token viene cifrato con una chiave del telefono e non è incluso nei backup.")
            .setNegativeButton("Annulla",null).setPositiveButton("Salva") { _,_ ->
                message=try { settings.save(url.text.toString(),token.text.toString());"Server configurato." }
                    catch(e:IllegalArgumentException) { e.message ?: "Configurazione non valida." }
                render()
            }.show()
    }
    private fun confirmUpload(workouts:List<Workout>) {
        AlertDialog.Builder(this).setTitle("Invia al tuo server")
            .setMessage("${workouts.size} allenamenti, con percorsi GPS precisi e serie (frequenza cardiaca, velocità, cadenza, potenza), saranno inviati a ${settings.serverUrl}. Gli allenamenti già inviati e non modificati vengono saltati.")
            .setNegativeButton("Annulla",null).setPositiveButton("Invia") { _,_ ->
                val url=settings.serverUrl ?: return@setPositiveButton
                runTask {
                    message="Invio di ${workouts.size} allenamenti…";render()
                    val report=withContext(Dispatchers.IO) {
                        val server=MoscoRunServer(url,settings.token() ?: error("Token assente: configurare di nuovo il server."))
                        WorkoutSync(server::upload,PreferencesSyncLedger(this@MainActivity)).run(workouts)
                    }
                    message=report.describe()
                }
            }.show()
    }
    private fun renderDetail(w:Workout) {
        button("← Torna alla verifica") { selected=null;render() }
        text(w.title,22f,true)
        text("Inizio: ${w.start}\nFine: ${w.end}\nOrigine: ${w.source}",13f)
        text("Percorso · ${w.routeState}",18f,true)
        text("${w.route.size} posizioni GPS")
        if(w.route.isNotEmpty()) {
            root.addView(RouteView(this,w.route),LinearLayout.LayoutParams(-1,dp(220)))
            text("Traccia locale schematica, senza cartografia. Verde: partenza; arancio: arrivo.",11f)
            if(canOpenGoogleRouteMap()) button("Apri mappa Google") {
                AlertDialog.Builder(this).setTitle("Mappa della corsa")
                    .setMessage("Per mostrare la cartografia, MoscoRun si collega a Google e richiede la mappa dell'area della corsa. Il tracciato verrà sovrapposto alla mappa. Puoi anche continuare a usare la traccia locale senza aprire Google Maps.")
                    .setNegativeButton("Resta qui",null)
                    .setPositiveButton("Apri mappa") { _,_ -> openGoogleRouteMap(this,w) }.show()
            }
            stats("GPS",w.route.map { it.time })
        }
        if(provider=="Health Connect" && w.routeState=="consenso_richiesto") button("Richiedi accesso a questo percorso") {
            pendingRoute=w;routes.launch(w.id)
        }
        w.series.forEach { series ->
            val c=card();text("${series.name} · ${series.status}",17f,true,c)
            if(series.samples.isNotEmpty() || series.rawRecords.isEmpty()) stats(series.name,series.samples.map { it.time },c)
            if(series.rawRecords.isNotEmpty()) text("${series.rawRecords.size} record sorgente disponibili nel JSON",12f,parent=c)
            if(series.rawRecords.isNotEmpty()) button("Vedi record originali",c) {
                showJson("Record di ${series.name}",series.rawRecords)
            }
            series.samples.firstOrNull()?.let { first ->
                text("Primo: ${first.value} ${first.unit} · ${first.time}\nUltimo: ${series.samples.last().value} ${first.unit} · ${series.samples.last().time}",12f,parent=c)
            }
            series.message?.let { text(it,12f,parent=c) }
        }
        button("Vedi campi originali della sessione") {
            showJson("Sessione originale",w.raw)
        }
        text("L'intervallo mediano descrive i timestamp disponibili. Non è una garanzia sulla frequenza del sensore e non dimostra continuità.",12f)
        reports[provider]?.let { report -> button("Esporta rapporto con questo dettaglio") { confirmExport(report) } }
    }
    private fun showJson(title:String,value:Any?) {
        val view=TextView(this).apply { text=Json.gson.toJson(value).take(60000);setTextIsSelectable(true);setPadding(24,24,24,24);textSize=12f }
        AlertDialog.Builder(this).setTitle("$title · anteprima fino a 60.000 caratteri")
            .setView(ScrollView(this).apply { addView(view) }).setPositiveButton("Chiudi",null).show()
    }
    private fun stats(name:String,times:List<String>,parent:LinearLayout=root) {
        val s=SamplingStats.calculate(times)
        text("${s.count} campioni · ${s.uniqueTimes} timestamp distinti\nIntervalli: mediana ${s.medianIntervalMs ?: "—"} ms · min ${s.minIntervalMs ?: "—"} · max ${s.maxIntervalMs ?: "—"}",12f,parent=parent)
    }
    private fun confirmExport(report:ProbeReport) {
        AlertDialog.Builder(this).setTitle("Salva il rapporto sul dispositivo")
            .setMessage("Il file contiene dati personali e può contenere coordinate GPS precise. Sarà salvato solo nella destinazione che scegli; non viene inviato da MoscoRun. Le anteprime delle categorie sono limitate; i dettagli degli allenamenti caricati sono inclusi.")
            .setNegativeButton("Annulla",null).setPositiveButton("Scegli destinazione") { _,_ ->
                pendingExport=Json.gson.toJson(report)
                export.launch("moscorun-probe-${Instant.now().epochSecond}.json")
            }.show()
    }
}
