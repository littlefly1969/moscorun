package it.moscorun.app.ui

import android.app.Activity
import android.content.Intent
import android.graphics.Color
import android.graphics.Typeface
import android.os.Bundle
import android.os.Handler
import android.os.Looper
import android.view.View
import android.view.WindowManager
import android.widget.*
import com.google.android.gms.common.ConnectionResult
import com.google.android.gms.common.GoogleApiAvailability
import com.google.android.gms.maps.CameraUpdateFactory
import com.google.android.gms.maps.GoogleMap
import com.google.android.gms.maps.GoogleMapOptions
import com.google.android.gms.maps.MapView
import com.google.android.gms.maps.model.BitmapDescriptorFactory
import com.google.android.gms.maps.model.LatLng
import com.google.android.gms.maps.model.LatLngBounds
import com.google.android.gms.maps.model.MarkerOptions
import com.google.android.gms.maps.model.PolylineOptions
import it.moscorun.app.model.TrackPoint

/** Opens the interactive map for a route already in memory; the points never leave the process. */
fun openGoogleRouteMap(activity: Activity, title: String, points: List<TrackPoint>) {
    RouteMapSession.title = title
    RouteMapSession.points = points
    activity.startActivity(Intent(activity, RouteMapActivity::class.java))
}

/** Interactive standard map: no Map ID, Places, route calculation or live location. */
class RouteMapActivity : Activity() {
    private var mapView: MapView? = null
    private var map: GoogleMap? = null
    private lateinit var status: TextView
    private lateinit var retry: Button
    private val handler = Handler(Looper.getMainLooper())
    private var loaded = false
    private var selectedType = 0
    private var coordinates: List<LatLng> = emptyList()
    private fun dp(value: Int) = (value * resources.displayMetrics.density).toInt()
    private val timeout = Runnable {
        if (!loaded && !isFinishing) {
            status.text = "Mappa non disponibile. Controlla la connessione e riprova."
            retry.visibility = View.VISIBLE
        }
    }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        window.addFlags(WindowManager.LayoutParams.FLAG_SECURE)
        selectedType = savedInstanceState?.getInt("mapType", 0) ?: 0
        val root = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            setBackgroundColor(Color.rgb(243, 246, 244))
        }
        setContentView(root)
        root.setOnApplyWindowInsetsListener { v, insets ->
            v.setPadding(insets.systemWindowInsetLeft, insets.systemWindowInsetTop,
                insets.systemWindowInsetRight, insets.systemWindowInsetBottom)
            insets
        }
        root.requestApplyInsets()
        root.addView(Button(this).apply {
            text = "← Torna all'allenamento"; isAllCaps = false
            setOnClickListener { finish() }
        })
        root.addView(TextView(this).apply {
            text = RouteMapSession.title.ifBlank { "Il tuo allenamento" }
            textSize = 22f; setTypeface(typeface, Typeface.BOLD)
            setTextColor(Color.rgb(23, 106, 82)); setPadding(dp(16), dp(8), dp(16), dp(4))
        })
        status = TextView(this).apply {
            text = "Caricamento mappa…"; textSize = 13f
            setPadding(dp(16), dp(4), dp(16), dp(8))
        }
        root.addView(status)
        retry = Button(this).apply {
            text = "Riprova"; isAllCaps = false; visibility = View.GONE
            setOnClickListener { recreate() }
        }
        root.addView(retry)

        val sections = RouteMapGeometry.segments(RouteMapSession.points)
        val points = sections.flatten()
        if (points.isEmpty()) {
            status.text = "Il percorso non è più disponibile in memoria. Torna all'allenamento e aprilo di nuovo."
            return
        }
        val services = GoogleApiAvailability.getInstance()
        val availability = services.isGooglePlayServicesAvailable(this)
        if (availability != ConnectionResult.SUCCESS) {
            status.text = "Per caricare la mappa, aggiorna o abilita Google Play Services sul telefono."
            retry.text = "Verifica Google Play Services"; retry.visibility = View.VISIBLE
            retry.setOnClickListener { services.getErrorDialog(this, availability, 100)?.show() }
            return
        }
        coordinates = points.map { LatLng(it.latitude, it.longitude) }
        val controls = LinearLayout(this).apply { orientation = LinearLayout.HORIZONTAL }
        val selector = Spinner(this).apply {
            adapter = ArrayAdapter(this@RouteMapActivity, android.R.layout.simple_spinner_dropdown_item,
                listOf("Stradale", "Satellite", "Rilievo"))
            setSelection(selectedType)
            onItemSelectedListener = object : AdapterView.OnItemSelectedListener {
                override fun onItemSelected(parent: AdapterView<*>?, view: View?, position: Int, id: Long) {
                    selectedType = position; applyMapType()
                }
                override fun onNothingSelected(parent: AdapterView<*>?) {}
            }
        }
        controls.addView(selector, LinearLayout.LayoutParams(0, -2, 1f))
        controls.addView(Button(this).apply {
            text = "Centra percorso"; isAllCaps = false
            setOnClickListener { fitRoute() }
        })
        root.addView(controls)
        val view = MapView(this, GoogleMapOptions().mapType(GoogleMap.MAP_TYPE_NORMAL))
        mapView = view
        view.contentDescription = "Mappa interattiva del percorso dell'allenamento"
        root.addView(view, LinearLayout.LayoutParams(-1, 0, 1f))
        root.addView(TextView(this).apply {
            text = "Verde: partenza · arancio: arrivo"
            textSize = 12f; setPadding(dp(16), dp(8), dp(16), dp(8))
        })
        view.onCreate(savedInstanceState?.getBundle("mapState"))
        handler.postDelayed(timeout, 20000)
        view.getMapAsync { googleMap ->
            if (isDestroyed || isFinishing) return@getMapAsync
            map = googleMap
            googleMap.uiSettings.apply {
                isZoomControlsEnabled = true
                isCompassEnabled = true
                isMapToolbarEnabled = false
            }
            applyMapType()
            for (segment in sections) {
                if (segment.size < 2) continue
                val line = segment.map { LatLng(it.latitude, it.longitude) }
                googleMap.addPolyline(PolylineOptions().addAll(line).color(Color.WHITE).width(dp(7).toFloat()).zIndex(1f))
                googleMap.addPolyline(PolylineOptions().addAll(line).color(Color.rgb(23, 106, 82)).width(dp(4).toFloat()).zIndex(2f))
            }
            coordinates.firstOrNull()?.let { first ->
                googleMap.addMarker(MarkerOptions().position(first).title("Partenza")
                    .icon(BitmapDescriptorFactory.defaultMarker(BitmapDescriptorFactory.HUE_GREEN)))
            }
            if (coordinates.size > 1) googleMap.addMarker(MarkerOptions().position(coordinates.last()).title("Arrivo")
                .icon(BitmapDescriptorFactory.defaultMarker(BitmapDescriptorFactory.HUE_ORANGE)))
            googleMap.setOnMapLoadedCallback {
                if (!isDestroyed && !isFinishing) {
                    loaded = true; handler.removeCallbacks(timeout); retry.visibility = View.GONE
                    val excluded = RouteMapSession.points.size - points.size
                    status.text = "${points.size} punti GPS" +
                            if (excluded > 0) " · $excluded punti non validi esclusi" else " · percorso registrato"
                }
            }
            if (savedInstanceState == null) fitRoute()
        }
    }

    private fun applyMapType() {
        map?.mapType = when (selectedType) {
            1 -> GoogleMap.MAP_TYPE_HYBRID
            2 -> GoogleMap.MAP_TYPE_TERRAIN
            else -> GoogleMap.MAP_TYPE_NORMAL
        }
    }

    private fun fitRoute() {
        val view = mapView ?: return
        view.post {
            val current = map ?: return@post
            if (isDestroyed) return@post
            if (coordinates.isEmpty()) return@post
            val first = coordinates.first()
            if (coordinates.all { it == first } || view.width <= 0 || view.height <= 0) {
                current.moveCamera(CameraUpdateFactory.newLatLngZoom(first, 15f))
            } else {
                val builder = LatLngBounds.builder()
                coordinates.forEach { builder.include(it) }
                val padding = minOf(dp(36), minOf(view.width, view.height) / 4)
                current.moveCamera(CameraUpdateFactory.newLatLngBounds(builder.build(), view.width, view.height, padding))
            }
        }
    }

    override fun onStart() { super.onStart(); mapView?.onStart() }
    override fun onResume() { super.onResume(); mapView?.onResume() }
    override fun onPause() { mapView?.onPause(); super.onPause() }
    override fun onStop() { mapView?.onStop(); super.onStop() }
    override fun onLowMemory() { super.onLowMemory(); mapView?.onLowMemory() }
    override fun onSaveInstanceState(state: Bundle) {
        super.onSaveInstanceState(state)
        state.putInt("mapType", selectedType)
        val mapState = Bundle(); mapView?.onSaveInstanceState(mapState); state.putBundle("mapState", mapState)
    }
    override fun onDestroy() {
        handler.removeCallbacks(timeout); map = null; mapView?.onDestroy(); mapView = null
        if (isFinishing) RouteMapSession.clear()
        super.onDestroy()
    }
}
