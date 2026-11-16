package it.moscorun.app.ui

import android.app.AlertDialog
import android.view.View
import android.widget.LinearLayout
import it.moscorun.app.server.MetricDisplay
import it.moscorun.app.server.MoscoRunServer
import it.moscorun.app.server.ReadResult
import it.moscorun.app.server.RemoteDetail
import it.moscorun.app.server.RemoteRoute
import it.moscorun.app.server.RemoteSeries
import it.moscorun.app.server.RemoteSeriesInfo
import it.moscorun.app.server.RemoteWorkout
import it.moscorun.app.server.WorkoutFormat
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext

/**
 * One workout as the server holds it. The summary comes from the list; the series list and the route are read
 * when the screen opens, each chart only when asked. Nothing is read from the phone's health data here.
 */
class DetailScreen(private val summary: RemoteWorkout) : Screen() {
    private var detail: ReadResult<RemoteDetail>? = null
    private var route: ReadResult<RemoteRoute>? = null
    private val charts = LinkedHashMap<String, ReadResult<RemoteSeries?>>()
    private val loading = HashSet<String>()

    override fun build(): View {
        val server = host.server()
        val workout = (detail as? ReadResult.Ok)?.value?.workout ?: summary
        val view = page {
            action("← Allenamenti") { host.back() }
            heading(WorkoutFormat.source(workout.source).ifEmpty { "Dal tuo server" }, WorkoutFormat.activity(workout.activityType))
            label(WorkoutFormat.dateTime(workout.startedAt), 14f, color = Palette.muted)
            card {
                label("Riepilogo", 17f, bold = true)
                row("Distanza", WorkoutFormat.distance(workout.distanceM))
                row("Durata", WorkoutFormat.duration(workout.seconds))
                row("Passo medio", WorkoutFormat.pace(workout.seconds, workout.distanceM))
                row("Frequenza cardiaca media", WorkoutFormat.bpm(workout.averageHeartRate))
                row("Frequenza cardiaca massima", WorkoutFormat.bpm(workout.maxHeartRate))
                row("Energia", WorkoutFormat.energy(workout.energyKcal))
            }
            if (server == null) {
                label("Server non configurato.", 14f, color = Palette.warning)
                return@page
            }
            routeCard(workout)
            when (val loaded = detail) {
                null -> label("Caricamento del dettaglio…", 13f, color = Palette.muted)
                is ReadResult.Failed -> card {
                    label(WorkoutsScreen.describe(loaded), 14f, color = Palette.warning)
                    action("Riprova") { detail = null; host.refresh() }
                }
                is ReadResult.Ok -> {
                    if (loaded.value.series.isEmpty()) label("Il server non ha serie per questo allenamento.", 13f, color = Palette.muted)
                    loaded.value.series.forEach { seriesCard(server, it) }
                }
            }
        }
        if (server != null) {
            if (detail == null) load("detail") { detail = server.detail(summary.id) }
            if (route == null && summary.trackPointCount > 0) load("route") { route = server.route(summary.id) }
        }
        return view
    }

    private fun LinearLayout.row(name: String, value: String) {
        if (value != "—") label("$name: $value", 14f)
    }

    private fun LinearLayout.routeCard(workout: RemoteWorkout) {
        if (workout.trackPointCount == 0) return
        card {
            label("Percorso", 17f, bold = true)
            when (val loaded = route) {
                null -> label("Caricamento del percorso…", 13f, color = Palette.muted)
                is ReadResult.Failed -> {
                    label(if (loaded.kind == it.moscorun.app.server.Failure.NOT_FOUND) "Il percorso non è disponibile (assente o nascosto dalle zone private del server)."
                        else WorkoutsScreen.describe(loaded), 14f, color = Palette.warning)
                    action("Riprova") { route = null; host.refresh() }
                }
                is ReadResult.Ok -> {
                    val value = loaded.value
                    if (value.points.isEmpty()) { label("Nessun punto visibile.", 13f, color = Palette.muted); return@card }
                    addView(RouteView(context, value.points), LinearLayout.LayoutParams(-1, context.dp(220)))
                    val hidden = value.hiddenStartPoints + value.hiddenEndPoints
                    label("${value.points.size} punti mostrati su ${value.totalPoints}" +
                        if (hidden > 0) " · $hidden nascosti dalle zone private" else "", 12f, color = Palette.muted)
                    action("Apri su Google Maps") {
                        AlertDialog.Builder(context)
                            .setTitle("Mappa di Google")
                            .setMessage("Per mostrare la cartografia l'app si collega a Google e scarica le mappe dell'area del percorso. Il tracciato viene disegnato sul telefono.")
                            .setPositiveButton("Apri") { _, _ -> host.openMap(WorkoutFormat.activity(workout.activityType), value.points) }
                            .setNegativeButton("Annulla", null).show()
                    }
                }
            }
        }
    }

    private fun LinearLayout.seriesCard(server: MoscoRunServer, info: RemoteSeriesInfo) {
        val display = MetricDisplay.of(info.metric, info.unit)
        card {
            label(display.label, 17f, bold = true)
            when (val loaded = charts[info.metric]) {
                null -> {
                    label("${info.sampleCount} campioni sul server", 12f, color = Palette.muted)
                    if (info.metric in loading) label("Caricamento…", 13f, color = Palette.muted)
                    else action("Mostra grafico") { load(info.metric) { charts[info.metric] = server.series(summary.id, info.metric, CHART_POINTS) } }
                }
                is ReadResult.Failed -> {
                    label(WorkoutsScreen.describe(loaded), 14f, color = Palette.warning)
                    action("Riprova") { charts.remove(info.metric); host.refresh() }
                }
                is ReadResult.Ok -> {
                    val series = loaded.value
                    val stats = series?.let { ChartGeometry.stats(it.values) }
                    if (series == null || stats == null) { label("Nessun campione.", 13f, color = Palette.muted); return@card }
                    addView(SeriesChartView(context, series.times, series.values), LinearLayout.LayoutParams(-1, context.dp(160)))
                    label("min ${display.format(stats.first)} · media ${display.format(stats.second)} · max ${display.format(stats.third)}", 13f)
                    if (series.values.size < series.totalSamples)
                        label("Grafico ridotto dal server a ${series.values.size} punti su ${series.totalSamples}; minimi e massimi sono quelli reali.", 12f, color = Palette.muted)
                }
            }
        }
    }

    /** Runs one read off the main thread and shows the screen again; a second request for the same thing is ignored. */
    private fun load(key: String, read: () -> Unit) {
        if (!loading.add(key)) return
        if (key != "detail" && key != "route") host.refresh()
        // The read belongs to the activity: rebuilding the view (for another chart, for instance) must not lose it.
        host.scope.launch {
            try { withContext(Dispatchers.IO) { read() } } finally { loading.remove(key) }
            if (attached) host.refresh()
        }
    }

    private companion object {
        const val CHART_POINTS = 600
    }
}
