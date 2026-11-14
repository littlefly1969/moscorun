package it.moscorun.probe

import android.app.Activity
import android.content.Intent

fun canOpenGoogleRouteMap() = true

// Keep private route samples in memory; do not put large tracks in Binder extras or files.
internal object RouteMapSession {
    var title = ""
    var points: List<TrackPoint> = emptyList()
    var preview = false
    fun clear() { title = ""; points = emptyList(); preview = false }
}

fun openGoogleMapPreview(activity: Activity) {
    RouteMapSession.clear()
    RouteMapSession.title = "Prova Google Maps"
    RouteMapSession.preview = true
    activity.startActivity(Intent(activity, RouteMapActivity::class.java))
}

fun openGoogleRouteMap(activity: Activity, workout: Workout) {
    RouteMapSession.title = workout.title
    RouteMapSession.points = workout.route.toList()
    RouteMapSession.preview = false
    activity.startActivity(Intent(activity, RouteMapActivity::class.java))
}

internal object RouteMapGeometry {
    // Invalid samples create gaps; don't invent a connecting line through missing data.
    fun segments(points: List<TrackPoint>): List<List<TrackPoint>> {
        val result = mutableListOf<List<TrackPoint>>()
        var segment = mutableListOf<TrackPoint>()
        for (point in points) {
            if (point.latitude.isFinite() && point.longitude.isFinite() &&
                point.latitude in -90.0..90.0 && point.longitude in -180.0..180.0) {
                segment.add(point)
            } else if (segment.isNotEmpty()) {
                result.add(segment); segment = mutableListOf()
            }
        }
        if (segment.isNotEmpty()) result.add(segment)
        return result
    }
}
