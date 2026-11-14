package it.moscorun.probe

import android.content.Context
import android.graphics.Canvas
import android.graphics.Color
import android.graphics.Paint
import android.graphics.Path
import android.view.View
import kotlin.math.cos
import kotlin.math.max

/** Local route sketch, no remote map tiles or network access. */
class RouteView(context:Context, private val points:List<TrackPoint>):View(context) {
    private val paint=Paint(Paint.ANTI_ALIAS_FLAG)
    override fun onDraw(canvas:Canvas) {
        super.onDraw(canvas)
        if(points.isEmpty()) return
        val origin=points.first()
        val cosLat=cos(Math.toRadians(origin.latitude))
        // Unwrap longitude around the start to avoid a jump at the date line.
        val xy=points.map { point ->
            var delta=point.longitude-origin.longitude
            while(delta>180) delta-=360
            while(delta< -180) delta+=360
            (delta*cosLat) to (point.latitude-origin.latitude)
        }
        val minX=xy.minOf { it.first };val maxX=xy.maxOf { it.first }
        val minY=xy.minOf { it.second };val maxY=xy.maxOf { it.second }
        val scale=minOf((width-48)/max(maxX-minX,0.00001),(height-48)/max(maxY-minY,0.00001))
        fun x(v:Double)=((v-(minX+maxX)/2)*scale+width/2).toFloat()
        fun y(v:Double)=(-(v-(minY+maxY)/2)*scale+height/2).toFloat()
        val path=Path()
        xy.forEachIndexed { i,p -> if(i==0) path.moveTo(x(p.first),y(p.second)) else path.lineTo(x(p.first),y(p.second)) }
        paint.color=Color.rgb(23,106,82);paint.strokeWidth=5f;paint.style=Paint.Style.STROKE
        canvas.drawPath(path,paint)
        paint.style=Paint.Style.FILL
        canvas.drawCircle(x(xy.first().first),y(xy.first().second),8f,paint)
        paint.color=Color.rgb(195,91,58)
        canvas.drawCircle(x(xy.last().first),y(xy.last().second),8f,paint)
    }
}
