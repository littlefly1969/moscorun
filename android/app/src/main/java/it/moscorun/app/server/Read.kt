package it.moscorun.app.server

import com.google.gson.JsonElement
import com.google.gson.JsonObject
import com.google.gson.JsonParser
import it.moscorun.app.model.TrackPoint
import java.time.Instant
import java.time.ZoneId
import java.time.format.DateTimeFormatter
import java.util.Locale

/** Workout summary as stored on the MoscoRun server (WorkoutSummaryDto). */
data class RemoteWorkout(val id: String, val source: String, val sourceId: String, val activityType: String,
    val startedAt: String, val endedAt: String, val activeDurationSeconds: Double?, val distanceM: Double?,
    val energyKcal: Double?, val averageHeartRate: Double?, val maxHeartRate: Double?, val trackPointCount: Int) {
    /** Active time when the source reports it, otherwise the whole session. */
    val seconds: Double get() = activeDurationSeconds
        ?: ((Instant.parse(endedAt).toEpochMilli() - Instant.parse(startedAt).toEpochMilli()) / 1000.0)
}

/** One page of the server list; [nextCursor] is null on the last page (and on servers without pagination). */
data class WorkoutPage(val items: List<RemoteWorkout>, val nextCursor: String?)

/** A series the server holds for a workout, as listed by the detail endpoint. */
data class RemoteSeriesInfo(val metric: String, val unit: String, val sampleCount: Int)

data class RemoteDetail(val workout: RemoteWorkout, val series: List<RemoteSeriesInfo>)

/** Samples of one metric; fewer than [totalSamples] when the server reduced them for a chart. */
class RemoteSeries(val metric: String, val unit: String, val times: LongArray, val values: DoubleArray, val totalSamples: Int)

/**
 * Route returned by the server (GeoJSON Feature). Points hidden by the server's privacy zones are not sent:
 * the counts only say how many were removed at the start and at the end.
 */
data class RemoteRoute(val points: List<TrackPoint>, val totalPoints: Int, val hiddenStartPoints: Int, val hiddenEndPoints: Int)

/** Who the device token belongs to: [accountId] keeps the sync state of different people apart. */
data class DeviceIdentity(val deviceId: String, val name: String, val kind: String, val accountId: String)

enum class Failure {
    /** 401: token unknown or revoked. */
    UNAUTHORIZED,

    /** 403: the token is valid but cannot do this (e.g. a scale token reading workouts). */
    FORBIDDEN,

    /** 404: not on the server, hidden by privacy zones, or an endpoint this server version lacks. */
    NOT_FOUND,

    /** No answer: offline, timeout, DNS or TLS failure. */
    OFFLINE,

    /** 429 and 5xx: the server answered but cannot serve now. */
    UNAVAILABLE,

    /** Anything else, including answers that cannot be parsed. */
    UNEXPECTED,
}

sealed interface ReadResult<out T> {
    data class Ok<T>(val value: T) : ReadResult<T>
    data class Failed(val message: String, val kind: Failure = Failure.UNEXPECTED) : ReadResult<Nothing> {
        /** The token must be replaced before anything else can work. */
        val unauthorized: Boolean get() = kind == Failure.UNAUTHORIZED || kind == Failure.FORBIDDEN

        /** Trying again later can succeed without the user doing anything. */
        val transient: Boolean get() = kind == Failure.OFFLINE || kind == Failure.UNAVAILABLE
    }
}

/** Parses the JSON of the read endpoints; malformed list entries are skipped rather than failing the whole list. */
object ServerReadParser {
    private val uuid = Regex("^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$")
    private val metricName = Regex("^[A-Za-z][A-Za-z0-9]{0,39}$")

    fun validId(id: String) = uuid.matches(id)

    /** Metric codes travel in a query string: only plain identifiers are accepted. */
    fun validMetric(metric: String) = metricName.matches(metric)

    fun page(json: String): WorkoutPage {
        val root = JsonParser.parseString(json).asJsonObject
        val items = root.getAsJsonArray("items").mapNotNull { runCatching { workout(it.asJsonObject) }.getOrNull() }
        // The server already orders by (startedAt, id) descending: the order of a page must not be changed here,
        // or consecutive pages would interleave.
        return WorkoutPage(items, root.string("nextCursor")?.takeIf { it.isNotEmpty() })
    }

    fun workout(o: JsonObject): RemoteWorkout {
        val id = o.string("id")!!
        require(validId(id)) { "Identificativo non valido." }
        return RemoteWorkout(id, o.string("source").orEmpty(), o.string("sourceId").orEmpty(), o.string("activityType")!!,
            Instant.parse(o.string("startedAt")!!).toString(), Instant.parse(o.string("endedAt")!!).toString(),
            o.double("activeDurationSeconds"), o.double("distanceM"), o.double("energyKcal"),
            o.double("averageHeartRate"), o.double("maxHeartRate"), o.get("trackPointCount")?.takeUnless(JsonElement::isJsonNull)?.asInt ?: 0)
    }

    fun detail(json: String): RemoteDetail {
        val root = JsonParser.parseString(json).asJsonObject
        val series = root.getAsJsonArray("series")?.mapNotNull { item ->
            runCatching {
                val o = item.asJsonObject
                RemoteSeriesInfo(o.string("metric")!!.also { require(validMetric(it)) }, o.string("unit").orEmpty(), o.get("sampleCount").asInt)
            }.getOrNull()
        }.orEmpty()
        return RemoteDetail(workout(root.getAsJsonObject("summary")), series)
    }

    /** Series of the answer, optionally only [metric]: older servers ignore the filter and return them all. */
    fun series(json: String, metric: String? = null): List<RemoteSeries> =
        JsonParser.parseString(json).asJsonObject.getAsJsonArray("items").map { item ->
            val o = item.asJsonObject
            val times = o.getAsJsonArray("times")
            val values = o.getAsJsonArray("values")
            require(times.size() == values.size()) { "Tempi e valori non allineati." }
            RemoteSeries(o.string("metric")!!, o.string("unit").orEmpty(), LongArray(times.size()) { times[it].asLong },
                DoubleArray(values.size()) { values[it].asDouble }, o.get("totalSamples")?.takeUnless(JsonElement::isJsonNull)?.asInt ?: times.size())
        }.filter { metric == null || it.metric == metric }

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

    fun identity(json: String): DeviceIdentity {
        val o = JsonParser.parseString(json).asJsonObject
        val account = o.string("accountId")!!
        require(validId(account)) { "Account non valido." }
        return DeviceIdentity(o.string("deviceId")!!, o.string("name").orEmpty(), o.string("kind").orEmpty(), account.lowercase())
    }

    private fun JsonObject.string(name: String): String? = get(name)?.takeUnless(JsonElement::isJsonNull)?.asString
    private fun JsonObject.double(name: String): Double? = get(name)?.takeUnless(JsonElement::isJsonNull)?.asDouble
}

/** Italian labels and units, consistent with the web dashboard. */
object WorkoutFormat {
    private val labels = mapOf("RUNNING" to "Corsa", "WALKING" to "Camminata", "HIKING" to "Escursione",
        "CYCLING" to "Bici", "SWIMMING" to "Nuoto", "TREADMILL" to "Tapis roulant")
    private val sources = mapOf("samsung-health" to "Samsung Health", "health-connect" to "Health Connect")
    private val italian = Locale.ITALIAN
    private val dateTime = DateTimeFormatter.ofPattern("EEE d MMM yyyy, HH:mm", italian)
    private val day = DateTimeFormatter.ofPattern("d MMM yyyy", italian)

    fun activity(code: String): String = labels[code.uppercase()]
        ?: code.replace(Regex("[_.:-]+"), " ").trim().lowercase().replaceFirstChar { it.titlecase(italian) }.ifEmpty { code }

    fun source(code: String): String = sources[code] ?: code

    fun dateTime(iso: String, zone: ZoneId = ZoneId.systemDefault()): String = dateTime.format(Instant.parse(iso).atZone(zone))

    fun dateTime(instant: Instant, zone: ZoneId = ZoneId.systemDefault()): String = dateTime.format(instant.atZone(zone))

    fun day(instant: Instant, zone: ZoneId = ZoneId.systemDefault()): String = day.format(instant.atZone(zone))

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

    fun energy(kcal: Double?): String = kcal?.let { "${Math.round(it)} kcal" } ?: "—"
}

/**
 * How a metric is shown. The list of metrics comes from the server: an unknown one is still displayed, with the
 * server's own name and unit, so a newer server never needs a new app to show its data.
 */
data class MetricDisplay(val label: String, val unit: String, val factor: Double, val decimals: Int) {
    fun format(serverValue: Double): String =
        String.format(Locale.ITALIAN, "%.${decimals}f %s", serverValue * factor, unit).trim()

    companion object {
        private val known = mapOf(
            "HeartRate" to MetricDisplay("Frequenza cardiaca", "bpm", 1.0, 0),
            // The server stores m/s; people read speed in km/h.
            "Speed" to MetricDisplay("Velocità", "km/h", 3.6, 1),
            "Cadence" to MetricDisplay("Cadenza", "passi/min", 1.0, 0),
            "Power" to MetricDisplay("Potenza", "W", 1.0, 0),
        )

        fun of(metric: String, serverUnit: String): MetricDisplay =
            known[metric] ?: MetricDisplay(metric, serverUnit, 1.0, 1)
    }
}
