package it.moscorun.app.ui

import org.junit.Assert.assertArrayEquals
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertThrows
import org.junit.Test

class ChartGeometryTest {
    @Test fun `samples span the drawing area with time to the right and larger values higher`() {
        val xy = ChartGeometry.points(longArrayOf(0, 50, 100), doubleArrayOf(100.0, 150.0, 200.0), 120f, 70f, 10f)

        assertArrayEquals(floatArrayOf(10f, 60f, 60f, 35f, 110f, 10f), xy, 0.001f)
    }

    @Test fun `a flat series and a single sample are centred`() {
        assertArrayEquals(floatArrayOf(10f, 35f, 110f, 35f),
            ChartGeometry.points(longArrayOf(0, 10), doubleArrayOf(5.0, 5.0), 120f, 70f, 10f), 0.001f)
        assertArrayEquals(floatArrayOf(60f, 35f),
            ChartGeometry.points(longArrayOf(7), doubleArrayOf(5.0), 120f, 70f, 10f), 0.001f)
    }

    @Test fun `values that are not numbers are left out, never drawn as zero`() {
        val xy = ChartGeometry.points(longArrayOf(0, 5, 10), doubleArrayOf(1.0, Double.NaN, 3.0), 120f, 70f, 10f)

        assertEquals(4, xy.size)
    }

    @Test fun `nothing is drawn without samples or without room`() {
        assertEquals(0, ChartGeometry.points(LongArray(0), DoubleArray(0), 120f, 70f, 10f).size)
        assertEquals(0, ChartGeometry.points(longArrayOf(1), doubleArrayOf(1.0), 10f, 10f, 10f).size)
    }

    @Test fun `misaligned arrays are refused`() {
        assertThrows(IllegalArgumentException::class.java) { ChartGeometry.points(longArrayOf(1, 2), doubleArrayOf(1.0), 100f, 100f, 0f) }
    }

    @Test fun `statistics use real values only`() {
        assertEquals(Triple(1.0, 2.0, 3.0), ChartGeometry.stats(doubleArrayOf(1.0, Double.NaN, 3.0)))
        assertNull(ChartGeometry.stats(doubleArrayOf(Double.NaN)))
    }
}
