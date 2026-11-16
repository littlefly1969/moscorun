package it.moscorun.app.model

import java.time.Instant

/** Where workouts are read from on the phone. [code] is the `source` stored by the MoscoRun server. */
enum class Provider(val code: String, val label: String) {
    SAMSUNG_HEALTH("samsung-health", "Samsung Health"),
    HEALTH_CONNECT("health-connect", "Health Connect");

    companion object {
        /** New installations start from the Samsung Health Data SDK, which carries the richest data. */
        val DEFAULT = SAMSUNG_HEALTH

        fun fromName(name: String?): Provider? = entries.firstOrNull { it.name == name }
        fun fromCode(code: String?): Provider? = entries.firstOrNull { it.code == code }
    }
}

data class TrackPoint(
    val time: String, val latitude: Double, val longitude: Double,
    val altitudeMeters: Double? = null, val accuracyMeters: Double? = null,
)

data class MetricSample(val time: String, val value: Double)

/** [metric] is the server code (HeartRate, Speed, Cadence, Power). */
data class MetricSeries(val metric: String, val samples: List<MetricSample>)

/**
 * One workout as read from a source, complete with route and series. Only one page of these is ever kept in memory.
 * Values the source did not report stay null, never zero.
 */
data class SourceWorkout(
    val provider: Provider,
    /** Stable identity inside the source: it must not change between readings. */
    val sourceId: String,
    /** Grows when the source record changes, so the server replaces its copy. */
    val revision: Long,
    val activityType: String,
    val start: Instant,
    val end: Instant,
    val utcOffsetMinutes: Int? = null,
    val activeDurationSeconds: Double? = null,
    val distanceM: Double? = null,
    val energyKcal: Double? = null,
    val averageHeartRate: Double? = null,
    val maxHeartRate: Double? = null,
    val route: List<TrackPoint> = emptyList(),
    val series: List<MetricSeries> = emptyList(),
    /** Position of the record in the source ordering: reading resumes from here after an interruption. */
    val cursor: Instant = start,
)
