package it.moscorun.app.ui

import it.moscorun.app.model.TrackPoint

import org.junit.Assert.*
import org.junit.Test

class RouteMapGeometryTest {
    private fun point(lat: Double, lon: Double) = TrackPoint("2026-11-09T12:00:00Z", lat, lon)

    @Test fun emptyRouteStaysEmpty() {
        assertTrue(RouteMapGeometry.segments(emptyList()).isEmpty())
    }

    @Test fun invalidSamplesCreateGapsRatherThanConnectingMissingData() {
        val start = point(41.9, 12.5)
        val finish = point(41.91, 12.51)
        val result = RouteMapGeometry.segments(listOf(start, point(Double.NaN, 12.5), finish))
        assertEquals(listOf(listOf(start), listOf(finish)), result)
    }

    @Test fun outOfRangeAndInfiniteCoordinatesAreRejected() {
        val invalid = listOf(point(91.0, 0.0), point(0.0, 181.0), point(Double.POSITIVE_INFINITY, 0.0),
            point(0.0, Double.NEGATIVE_INFINITY))
        assertTrue(RouteMapGeometry.segments(invalid).isEmpty())
    }

    @Test fun validSamplesAndDateLineCrossingsPreserveOrderAndEndpoints() {
        val points = listOf(point(0.0, 179.99), point(0.0, -179.99), point(-90.0, -180.0), point(90.0, 180.0))
        assertEquals(listOf(points), RouteMapGeometry.segments(points))
    }
}
