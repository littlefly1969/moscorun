package it.moscorun.app.ui

import android.content.Context
import android.graphics.Canvas
import android.graphics.Paint
import android.graphics.Path
import android.view.View

/** Pure geometry of a line chart, apart from the view so it can be tested without a screen. */
internal object ChartGeometry {
    /**
     * Pixel coordinates (x0, y0, x1, y1, …) of the samples inside a [width] × [height] area with [padding] on
     * every side. Time runs left to right, larger values are higher. A flat series is drawn at mid height and a
     * single sample in the middle. Samples that are not finite numbers are left out.
     */
    fun points(times: LongArray, values: DoubleArray, width: Float, height: Float, padding: Float): FloatArray {
        require(times.size == values.size) { "Tempi e valori non allineati." }
        val kept = values.indices.filter { values[it].isFinite() }
        if (kept.isEmpty() || width <= 2 * padding || height <= 2 * padding) return FloatArray(0)
        val t0 = kept.minOf { times[it] }
        val t1 = kept.maxOf { times[it] }
        val v0 = kept.minOf { values[it] }
        val v1 = kept.maxOf { values[it] }
        val w = width - 2 * padding
        val h = height - 2 * padding
        val out = FloatArray(kept.size * 2)
        kept.forEachIndexed { n, i ->
            val x = if (t1 == t0) 0.5 else (times[i] - t0).toDouble() / (t1 - t0)
            val y = if (v1 == v0) 0.5 else (values[i] - v0) / (v1 - v0)
            out[2 * n] = (padding + x * w).toFloat()
            out[2 * n + 1] = (padding + (1 - y) * h).toFloat()
        }
        return out
    }

    /** Minimum, mean and maximum of the finite values, or null when there are none. */
    fun stats(values: DoubleArray): Triple<Double, Double, Double>? {
        val finite = values.filter { it.isFinite() }
        if (finite.isEmpty()) return null
        return Triple(finite.min(), finite.average(), finite.max())
    }
}

/** Line chart of one metric as reduced by the server; it holds only the samples it draws. */
class SeriesChartView(context: Context, private val times: LongArray, private val values: DoubleArray) : View(context) {
    private val line = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        color = Palette.green
        style = Paint.Style.STROKE
        strokeWidth = context.dp(2).toFloat()
        strokeJoin = Paint.Join.ROUND
    }
    private val axis = Paint().apply { color = 0x22000000; strokeWidth = 1f }

    override fun onDraw(canvas: Canvas) {
        super.onDraw(canvas)
        val padding = context.dp(6).toFloat()
        canvas.drawLine(padding, height - padding, width - padding, height - padding, axis)
        val xy = ChartGeometry.points(times, values, width.toFloat(), height.toFloat(), padding)
        if (xy.size < 2) return
        if (xy.size == 2) {
            canvas.drawCircle(xy[0], xy[1], line.strokeWidth * 2, line)
            return
        }
        val path = Path().apply {
            moveTo(xy[0], xy[1])
            for (i in 2 until xy.size step 2) lineTo(xy[i], xy[i + 1])
        }
        canvas.drawPath(path, line)
    }
}
