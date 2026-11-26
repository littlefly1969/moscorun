package it.moscorun.app.science

import androidx.health.connect.client.records.SleepSessionRecord
import androidx.health.connect.client.records.Vo2MaxRecord
import java.time.Duration
import java.time.Instant

/** Identity and origin of a Health Connect record, independent of its type. */
data class RecordOrigin(val id: String, val lastModified: Instant, val app: String)

/**
 * Health Connect records reduced to plain values: the reader copies them out of the SDK types, the mapping below
 * turns them into observations. Keeping the two apart lets the mapping be tested without Health Connect.
 */
sealed interface ScienceRecord {
    val origin: RecordOrigin

    data class RestingHeartRate(override val origin: RecordOrigin, val time: Instant, val beatsPerMinute: Long) : ScienceRecord
    data class HrvRmssd(override val origin: RecordOrigin, val time: Instant, val millis: Double) : ScienceRecord
    data class OxygenSaturation(override val origin: RecordOrigin, val time: Instant, val percent: Double) : ScienceRecord
    data class RespiratoryRate(override val origin: RecordOrigin, val time: Instant, val perMinute: Double) : ScienceRecord
    data class Vo2Max(override val origin: RecordOrigin, val time: Instant, val value: Double, val method: Int) : ScienceRecord
    data class Sleep(override val origin: RecordOrigin, val start: Instant, val end: Instant, val stages: List<SleepStage>) : ScienceRecord
}

data class SleepStage(val start: Instant, val end: Instant, val type: Int)

/**
 * Conversion of Health Connect records into catalog observations (`hc-science-v1`). Nothing is derived beyond
 * what the source states: no efficiency, no sleep duration without stages, no RR from heart rate. Values outside
 * the physiological range of their metric are gaps written by a sensor without contact, and are left out.
 */
object HealthConnectScienceMapping {
    const val MAPPING_VERSION = "hc-science-v1"
    private const val MAX_TEXT = 200

    private val asleep = setOf(SleepSessionRecord.STAGE_TYPE_SLEEPING, SleepSessionRecord.STAGE_TYPE_LIGHT,
        SleepSessionRecord.STAGE_TYPE_DEEP, SleepSessionRecord.STAGE_TYPE_REM)

    private val vo2Methods = mapOf(
        Vo2MaxRecord.MEASUREMENT_METHOD_METABOLIC_CART to "metabolic-cart",
        Vo2MaxRecord.MEASUREMENT_METHOD_HEART_RATE_RATIO to "heart-rate-ratio",
        Vo2MaxRecord.MEASUREMENT_METHOD_COOPER_TEST to "cooper-test",
        Vo2MaxRecord.MEASUREMENT_METHOD_MULTISTAGE_FITNESS_TEST to "multistage-fitness-test",
        Vo2MaxRecord.MEASUREMENT_METHOD_ROCKPORT_FITNESS_TEST to "rockport-fitness-test")

    fun observations(record: ScienceRecord): List<ScienceObservation> = when (record) {
        is ScienceRecord.RestingHeartRate -> listOfNotNull(observation(record, "restingHeartRate", "bpm",
            record.beatsPerMinute.toDouble().takeIf { it in 20.0..200.0 }, record.time, ScienceMethod.PROVIDER_REPORTED,
            "RestingHeartRateRecord", "bpm", record.beatsPerMinute.toDouble()))
        is ScienceRecord.HrvRmssd -> listOfNotNull(observation(record, "hrvRmssd", "ms",
            record.millis.takeIf { it.isFinite() && it in 1.0..500.0 }, record.time, ScienceMethod.PROVIDER_REPORTED,
            "HeartRateVariabilityRmssdRecord", "ms", record.millis))
        is ScienceRecord.OxygenSaturation -> listOfNotNull(observation(record, "spo2", "%",
            record.percent.takeIf { it.isFinite() && it in 50.0..100.0 }, record.time, ScienceMethod.MEASURED,
            "OxygenSaturationRecord", "%", record.percent))
        is ScienceRecord.RespiratoryRate -> listOfNotNull(observation(record, "respirationRate", "1/min",
            record.perMinute.takeIf { it.isFinite() && it in 3.0..80.0 }, record.time, ScienceMethod.PROVIDER_REPORTED,
            "RespiratoryRateRecord", "1/min", record.perMinute))
        is ScienceRecord.Vo2Max -> vo2(record)
        is ScienceRecord.Sleep -> sleep(record)
    }

    private fun vo2(record: ScienceRecord.Vo2Max): List<ScienceObservation> {
        val method = vo2Methods[record.method] ?: "other"
        // A metabolic cart measures gas exchange; every other method is an estimate of the provider.
        val measured = record.method == Vo2MaxRecord.MEASUREMENT_METHOD_METABOLIC_CART
        return listOfNotNull(observation(record, "vo2max", "mL/kg/min",
            record.value.takeIf { it.isFinite() && it in 10.0..100.0 }, record.time,
            if (measured) ScienceMethod.MEASURED else ScienceMethod.PROVIDER_ESTIMATED,
            "Vo2MaxRecord", "mL/kg/min", record.value, algorithm = "${record.origin.app}:$method"))
    }

    private fun sleep(record: ScienceRecord.Sleep): List<ScienceObservation> {
        if (!record.end.isAfter(record.start)) return emptyList()
        val inBed = Duration.between(record.start, record.end).seconds.toDouble()
        if (inBed > Duration.ofHours(24).seconds) return emptyList()
        val result = mutableListOf<ScienceObservation>()
        observation(record, "sleepInBedDuration", "s", inBed, record.end, ScienceMethod.PROVIDER_REPORTED,
            "SleepSessionRecord", "s", inBed)?.let(result::add)
        // Sleep time only where the source states its stages; stages are clipped to the session they belong to.
        val asleepSeconds = record.stages.filter { it.type in asleep }.sumOf { stage ->
            val start = maxOf(stage.start, record.start)
            val end = minOf(stage.end, record.end)
            if (end.isAfter(start)) Duration.between(start, end).seconds else 0L
        }.toDouble()
        if (record.stages.isNotEmpty() && asleepSeconds > 0) {
            observation(record, "sleepDuration", "s", asleepSeconds, record.end, ScienceMethod.PROVIDER_REPORTED,
                "SleepSessionRecord.stages", "s", asleepSeconds)?.let(result::add)
        }
        return result
    }

    private fun observation(record: ScienceRecord, metric: String, unit: String, value: Double?, time: Instant,
        method: ScienceMethod, nativeMetric: String, nativeUnit: String, nativeValue: Double,
        algorithm: String = record.origin.app): ScienceObservation? {
        if (value == null || record.origin.id.isBlank()) return null
        return ScienceObservation(
            externalId = "hc:${record.origin.id}".take(512),
            revision = record.origin.lastModified.toEpochMilli().coerceAtLeast(0),
            metric = metric,
            unit = unit,
            value = value,
            time = time,
            method = method,
            providerAlgorithm = algorithm.take(MAX_TEXT).ifBlank { null },
            nativeMetric = nativeMetric,
            nativeUnit = nativeUnit,
            nativeValue = nativeValue.takeIf { it.isFinite() })
    }
}
