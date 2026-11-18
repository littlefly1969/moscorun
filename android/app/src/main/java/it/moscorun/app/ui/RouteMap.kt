package it.moscorun.app.ui

import it.moscorun.app.model.TrackPoint

// Keep private route samples in memory; do not put large tracks in Binder extras or files.
internal object RouteMapSession {
    var title = ""
    var points: List<TrackPoint> = emptyList()
    fun clear() { title = ""; points = emptyList() }
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
