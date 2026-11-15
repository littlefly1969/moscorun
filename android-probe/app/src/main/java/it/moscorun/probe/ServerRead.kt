package it.moscorun.probe

import com.google.gson.JsonElement
import com.google.gson.JsonObject
import com.google.gson.JsonParser
import java.time.Instant
import java.time.ZoneId
import java.time.format.DateTimeFormatter
import java.util.Locale

/** Workout summary as stored on the MoscoRun server (WorkoutSummaryDto). */
data class RemoteWorkout(val id: String, val activityType: String, val startedAt: String, val endedAt: String,
    val activeDurationSeconds: Double?, val distanceM: Double?, val averageHeartRate: Double?, val maxHeartRate: Double?,
    val trackPointCount: Int) {
    /** Active time when the source reports it, otherwise the whole session. */
    val seconds: Double get() = activeDurationSeconds
        ?: (Instant.parse(endedAt).toEpochMilli() - Instant.parse(startedAt).toEpochMilli()) / 1000.0
}

/**
 * Route returned by the server (GeoJSON Feature). Points hidden by the server's privacy zones are not sent:
 * the counts only say how many were removed at the start and at the end.
 */
data class RemoteRoute(val points: List<TrackPoint>, val totalPoints: Int, val hiddenStartPoints: Int, val hiddenEndPoints: Int)

sealed interface ReadResult<out T> {
    data class Ok<T>(val value: T) : ReadResult<T>
    data class Failed(val message: String, val unauthorized: Boolean = false) : ReadResult<Nothing>
}

/** Parses the JSON of the read endpoints; malformed entries are skipped rather than failing the whole list. */
object ServerReadParser {
    private val uuid = Regex("^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$")

    fun validId(id: String) = uuid.matches(id)

    fun workouts(json: String): List<RemoteWorkout> =
        JsonParser.parseString(json).asJsonObject.getAsJsonArray("items").mapNotNull { item ->
            runCatching {
                val o = item.asJsonObject
                val id = o.string("id")!!
                require(validId(id))
                RemoteWorkout(id, o.string("activityType")!!, Instant.parse(o.string("startedAt")!!).toString(),
                    Instant.parse(o.string("endedAt")!!).toString(), o.double("activeDurationSeconds"), o.double("distanceM"),
                    o.double("averageHeartRate"), o.double("maxHeartRate"), o.get("trackPointCount")?.asInt ?: 0)
            }.getOrNull()
        }.sortedByDescending { it.startedAt }

    fun route(json: String): RemoteRoute {
        val feature = JsonParser.parseString(json).asJsonObject
        val properties = feature.getAsJsonObject("properties")
        val coordinates = feature.getAsJsonObject("geometry").getAsJsonArray("coordinates")
        val times = properties.getAsJsonArray("times")
        require(times.size() == coordinates.size()) { "Tempi e coordinate non allineati." }
        val points = coordinates.mapIndexed { i, c ->
            val position = c.asJsonArray
            TrackPoint(Instant.ofEpochMilli(times[i].asLong).toString(), latitude = position[1].asDouble, longitude = position[0].asDouble,
                altitudeMeters = if (position.size() > 2) position[2].asDouble else null)
        }
        return RemoteRoute(points, properties.get("totalPoints").asInt,
            properties.get("hiddenStartPoints")?.asInt ?: 0, properties.get("hiddenEndPoints")?.asInt ?: 0)
    }

    private fun JsonObject.string(name: String): String? = get(name)?.takeUnless(JsonElement::isJsonNull)?.asString
    private fun JsonObject.double(name: String): Double? = get(name)?.takeUnless(JsonElement::isJsonNull)?.asDouble
}

/** Italian labels and units, consistent with the web dashboard. */
object WorkoutFormat {
    private val labels = mapOf("RUNNING" to "Corsa", "WALKING" to "Camminata", "HIKING" to "Escursione",
        "CYCLING" to "Bici", "SWIMMING" to "Nuoto", "TREADMILL" to "Tapis roulant")
    private val italian = Locale.ITALIAN
    private val dateTime = DateTimeFormatter.ofPattern("EEE d MMM yyyy, HH:mm", italian)

    fun activity(code: String): String = labels[code.uppercase()]
        ?: code.replace(Regex("[_.:-]+"), " ").trim().lowercase().replaceFirstChar { it.titlecase(italian) }.ifEmpty { code }

    fun dateTime(iso: String, zone: ZoneId = ZoneId.systemDefault()): String = dateTime.format(Instant.parse(iso).atZone(zone))

    fun distance(meters: Double?): String = when {
        meters == null -> "—"
        meters < 1000 -> "${Math.round(meters)} m"
        else -> String.format(italian, "%.1f km", meters / 1000)
    }

    fun duration(seconds: Double): String {
        val total = Math.round(seconds)
        val h = total / 3600
        val m = total % 3600 / 60
        val s = total % 60
        return if (h > 0) String.format(italian, "%d:%02d:%02d", h, m, s) else String.format(italian, "%d:%02d", m, s)
    }

    /** Pace in min/km; meaningful only from 100 m. */
    fun pace(seconds: Double, meters: Double?): String {
        if (meters == null || meters < 100 || seconds <= 0) return "—"
        val perKm = Math.round(seconds / (meters / 1000))
        return String.format(italian, "%d:%02d /km", perKm / 60, perKm % 60)
    }

    fun bpm(value: Double?): String = value?.let { "${Math.round(it)} bpm" } ?: "—"
}
