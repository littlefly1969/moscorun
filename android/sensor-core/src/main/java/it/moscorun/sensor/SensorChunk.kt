package it.moscorun.sensor

import com.google.gson.JsonArray
import com.google.gson.JsonNull
import com.google.gson.JsonObject
import java.time.Instant
import java.util.UUID

data class SensorSample(val timeMillis: Long, val value: Double?)
data class SensorChannel(val metric: String, val unit: String, val field: String, val measured: Boolean = true)
data class SensorPayload(val id: String, val json: String, val samples: Int)

/** No interpolation or invented timestamps. Duplicate/non-monotonic timestamps start a new chunk. */
object SensorChunk {
    const val SOURCE = "samsung-sensor"
    const val MAX_SAMPLES = 16_384

    fun encode(session: UUID, channel: SensorChannel, points: List<SensorSample>, nowMillis: Long,
        chunkSize: Int = 1024): List<SensorPayload> {
        require(chunkSize in 1..MAX_SAMPLES)
        require(points.all { it.timeMillis > 0 && it.timeMillis <= nowMillis }) { "Timestamp SDK non valido o orologio non allineato" }
        val result = mutableListOf<SensorPayload>()
        var block = mutableListOf<SensorSample>()
        fun emit() {
            if (block.isEmpty()) return
            val id = "watch:$session:${UUID.randomUUID()}"
            val obj = JsonObject().apply {
                addProperty("source", SOURCE)
                addProperty("externalId", id)
                addProperty("revision", "1")
                addProperty("revisionOrder", 1)
                addProperty("metric", channel.metric)
                addProperty("unit", channel.unit)
                addProperty("method", if (channel.measured) "Measured" else "ProviderReported")
                addProperty("providerAlgorithm", "SamsungSensorSDK-1.4.1:" + channel.field)
                add("samples", JsonArray().apply {
                    block.forEach { p ->
                        add(JsonObject().apply {
                            addProperty("time", Instant.ofEpochMilli(p.timeMillis).toString())
                            if (p.value == null || !p.value.isFinite()) add("value", JsonNull.INSTANCE)
                            else addProperty("value", p.value)
                        })
                    }
                })
            }
            result += SensorPayload(id, obj.toString(), block.size)
            block = mutableListOf()
        }
        for (point in points) {
            if (block.size == chunkSize || (block.isNotEmpty() && point.timeMillis <= block.last().timeMillis)) emit()
            block.add(point)
        }
        emit()
        return result
    }

    /** Documented Samsung accelerometer conversion, raw counts are retained separately. */
    fun acceleration(raw: Int): Double = (9.81 / (16383.75 / 4.0)) * raw
}
