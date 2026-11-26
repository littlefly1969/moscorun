package it.moscorun.app.server

import com.google.gson.GsonBuilder
import it.moscorun.app.model.SourceWorkout
import java.security.MessageDigest
import java.time.Duration

/** Body of POST /api/v1/ingest/workouts (contracts/moscorun-api.json, IngestWorkoutRequest). */
@WireFormat data class UploadPoint(val time: String, val lat: Double, val lon: Double, val altitudeM: Double?, val accuracyM: Double?)
@WireFormat data class UploadSample(val time: String, val value: Double)
@WireFormat data class UploadSeries(val metric: String, val samples: List<UploadSample>)
@WireFormat data class UploadPayload(val source: String, val sourceId: String, val revision: Long, val activityType: String,
    val startedAt: String, val endedAt: String, val utcOffsetMinutes: Int?, val activeDurationSeconds: Double?,
    val distanceM: Double?, val energyKcal: Double?, val averageHeartRate: Double?, val maxHeartRate: Double?,
    val track: List<UploadPoint>, val series: List<UploadSeries>)

object UploadMapper {
    private val idChars = Regex("^[A-Za-z0-9_.:-]{1,128}$")
    private val ranges = mapOf("HeartRate" to 20.0..260.0, "Speed" to 0.0..100.0, "Cadence" to 0.0..400.0, "Power" to 0.0..3000.0)
    private val compact = GsonBuilder().disableHtmlEscaping().create()

    /** Activity codes keep the source value; characters outside the server alphabet become '_'. */
    fun activityCode(raw: String) = raw.replace(Regex("[^A-Za-z0-9_.:-]"), "_").take(64).ifEmpty { "UNKNOWN" }

    /**
     * Converts a workout read on the phone into the server payload, or null when its identity cannot be stored.
     * Samples outside the physical range of their metric (e.g. heart rate 0 written by the watch while the sensor
     * had no contact) are gaps, not measurements: they are dropped instead of rejecting the whole workout.
     *
     * The JSON produced from this payload is fingerprinted to recognise unchanged workouts: field order and
     * formatting must stay stable across app versions, otherwise every workout would be sent again.
     */
    fun toPayload(w: SourceWorkout): UploadPayload? {
        if (!idChars.matches(w.sourceId) || !w.end.isAfter(w.start)) return null
        val series = w.series.mapNotNull { s ->
            val range = ranges[s.metric] ?: return@mapNotNull null
            UploadSeries(s.metric, s.samples.filter { it.value.isFinite() && it.value in range }.map { UploadSample(it.time, it.value) })
                .takeIf { it.samples.isNotEmpty() }
        }.distinctBy { it.metric }
        val sessionSeconds = Duration.between(w.start, w.end).toMillis() / 1000.0
        val heartRate = ranges.getValue("HeartRate")
        // Summary values outside the server range (e.g. 0 bpm when the watch did not measure) are missing, not data.
        return UploadPayload(w.provider.code, w.sourceId, w.revision.coerceAtLeast(0), activityCode(w.activityType),
            w.start.toString(), w.end.toString(),
            w.utcOffsetMinutes?.takeIf { it in -840..840 },
            w.activeDurationSeconds?.takeIf { it.isFinite() && it >= 0 && it <= sessionSeconds + 1 },
            w.distanceM?.takeIf { it.isFinite() && it in 0.0..1_000_000.0 },
            w.energyKcal?.takeIf { it.isFinite() && it in 0.0..50_000.0 },
            w.averageHeartRate?.takeIf { it in heartRate }, w.maxHeartRate?.takeIf { it in heartRate },
            w.route.filter { it.latitude.isFinite() && it.longitude.isFinite() }
                .map { UploadPoint(it.time, it.latitude, it.longitude, it.altitudeMeters?.takeIf(Double::isFinite),
                    it.accuracyMeters?.takeIf(Double::isFinite)) },
            series)
    }

    fun json(payload: UploadPayload): String = compact.toJson(payload)

    /** Fingerprint of what was sent: unchanged workouts are not uploaded again. */
    fun fingerprint(json: String): String =
        MessageDigest.getInstance("SHA-256").digest(json.toByteArray()).joinToString("") { "%02x".format(it) }
}

enum class UploadOutcome {
    CREATED, STORED,

    /** 409: same identity, different content, revision not greater. */
    CONFLICT,

    /** 400: the server rejects this content; sending it again cannot succeed. */
    INVALID,

    /** 413: the workout exceeds what the server (or a proxy in front of it) accepts. */
    TOO_LARGE,

    /** 401/403: the token is no longer accepted. */
    UNAUTHORIZED,

    /** 429, 5xx, timeouts and network errors: worth trying again later. */
    RETRY,
}

data class UploadResult(val outcome: UploadOutcome, val detail: String? = null, val serverId: String? = null)
