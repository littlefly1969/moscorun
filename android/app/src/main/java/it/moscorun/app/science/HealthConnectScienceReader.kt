package it.moscorun.app.science

import android.content.Context
import androidx.health.connect.client.HealthConnectClient
import androidx.health.connect.client.permission.HealthPermission
import androidx.health.connect.client.records.HeartRateRecord
import androidx.health.connect.client.records.WeightRecord
import androidx.health.connect.client.records.BodyFatRecord
import androidx.health.connect.client.records.LeanBodyMassRecord
import androidx.health.connect.client.records.BodyWaterMassRecord
import androidx.health.connect.client.records.BoneMassRecord
import androidx.health.connect.client.records.BasalMetabolicRateRecord
import androidx.health.connect.client.records.HeightRecord
import androidx.health.connect.client.records.BloodPressureRecord
import androidx.health.connect.client.records.BodyTemperatureRecord
import androidx.health.connect.client.records.BloodGlucoseRecord
import androidx.health.connect.client.records.StepsRecord
import androidx.health.connect.client.records.DistanceRecord
import androidx.health.connect.client.records.FloorsClimbedRecord
import androidx.health.connect.client.records.TotalCaloriesBurnedRecord
import androidx.health.connect.client.records.ActiveCaloriesBurnedRecord
import androidx.health.connect.client.records.NutritionRecord
import androidx.health.connect.client.records.HydrationRecord
import androidx.health.connect.client.records.HeartRateVariabilityRmssdRecord
import androidx.health.connect.client.records.OxygenSaturationRecord
import androidx.health.connect.client.records.Record
import androidx.health.connect.client.records.RespiratoryRateRecord
import androidx.health.connect.client.records.RestingHeartRateRecord
import androidx.health.connect.client.records.SleepSessionRecord
import androidx.health.connect.client.records.Vo2MaxRecord
import androidx.health.connect.client.records.metadata.Metadata
import androidx.health.connect.client.request.ReadRecordsRequest
import androidx.health.connect.client.time.TimeRangeFilter
import it.moscorun.app.source.HealthConnectSource
import kotlinx.coroutines.CancellationException
import java.time.Instant
import kotlin.reflect.KClass

/**
 * Daily scientific data read from Health Connect, whatever the source of the workouts: Samsung Health, Garmin
 * Connect and other apps write sleep, resting heart rate, SpO2 and HRV there. The app that wrote each record is
 * kept as provider, so origins stay distinguishable on the server. Every permission is optional.
 */
class HealthConnectScienceReader(context: Context) : ScienceReader {
    private val appContext = context.applicationContext
    private val client get() = HealthConnectClient.getOrCreate(appContext)

    override val source = "health-connect"
    override val isolatedStreams = true
    override val supportedMetrics get() = ScienceCapabilities.healthConnect
    override fun checkpoint(availability: ScienceAvailability) = ScienceCapabilities.checkpoint(source, "hc-expanded-v1", availability)

    override suspend fun availability(): ScienceAvailability {
        if (HealthConnectClient.getSdkStatus(appContext) != HealthConnectClient.SDK_AVAILABLE)
            return ScienceAvailability(emptySet(), message = "Health Connect non è disponibile: i dati scientifici non vengono letti.", permissionsKnown = false)
        val granted = try {
            client.permissionController.getGrantedPermissions()
        } catch (e: CancellationException) {
            throw e
        } catch (e: Exception) {
            return ScienceAvailability(emptySet(), message = "Health Connect non risponde (${e.javaClass.simpleName}).", permissionsKnown = false)
        }
        val metrics = grantedMetrics(granted)
        val message = when {
            metrics.isEmpty() -> "Nessun permesso per i dati scientifici in Health Connect: concedili dalle Impostazioni di MoscoRun."
            HealthConnectSource.READ_IN_BACKGROUND !in granted -> "Senza l'accesso in background i dati scientifici si leggono solo con MoscoRun aperta."
            else -> null
        }
        return ScienceAvailability(metrics, HealthConnectSource.READ_HISTORY in granted, message)
    }

    /** Scientific permissions not granted yet, to ask from a visible activity; empty without Health Connect. */
    suspend fun requestable(): Set<String> {
        if (HealthConnectClient.getSdkStatus(appContext) != HealthConnectClient.SDK_AVAILABLE) return emptySet()
        return try {
            permissions - client.permissionController.getGrantedPermissions()
        } catch (e: CancellationException) {
            throw e
        } catch (_: Exception) {
            permissions
        }
    }

    override suspend fun read(from: Instant, to: Instant, metrics: Set<ScienceMetric>): List<ScienceObservation> {
        val range = TimeRangeFilter.between(from, to)
        val records = buildList<ScienceRecord> {
            if (ScienceMetric.RESTING_HEART_RATE in metrics) all(RestingHeartRateRecord::class, range).mapTo(this) {
                ScienceRecord.RestingHeartRate(origin(it.metadata), it.time, it.beatsPerMinute)
            }
            if (ScienceMetric.HRV_RMSSD in metrics) all(HeartRateVariabilityRmssdRecord::class, range).mapTo(this) {
                ScienceRecord.HrvRmssd(origin(it.metadata), it.time, it.heartRateVariabilityMillis)
            }
            if (ScienceMetric.OXYGEN_SATURATION in metrics) all(OxygenSaturationRecord::class, range).mapTo(this) {
                ScienceRecord.OxygenSaturation(origin(it.metadata), it.time, it.percentage.value)
            }
            if (ScienceMetric.RESPIRATORY_RATE in metrics) all(RespiratoryRateRecord::class, range).mapTo(this) {
                ScienceRecord.RespiratoryRate(origin(it.metadata), it.time, it.rate)
            }
            if (ScienceMetric.VO2_MAX in metrics) all(Vo2MaxRecord::class, range).mapTo(this) {
                ScienceRecord.Vo2Max(origin(it.metadata), it.time, it.vo2MillilitersPerMinuteKilogram, it.measurementMethod)
            }
            if (ScienceMetric.SLEEP in metrics) all(SleepSessionRecord::class, range).mapTo(this) {
                ScienceRecord.Sleep(origin(it.metadata), it.startTime, it.endTime, it.stages.map { s -> SleepStage(s.startTime, s.endTime, s.stage) })
            }
        }
        // Each observation belongs to one window by its time (a sleep session by its end), so windows never overlap.
        return (records.flatMap(HealthConnectScienceMapping::observations) + extended(range, metrics))
            .filter { !it.time.isBefore(from) && it.time.isBefore(to) }
    }

    private fun value(metadata: Metadata, metric: String, unit: String, amount: Double?, time: Instant,
        nativeMetric: String, method: ScienceMethod = ScienceMethod.PROVIDER_REPORTED, suffix: String = "",
        nativeUnit: String = unit, nativeValue: Double? = amount) =
        SdkObservationMapping.value(origin(metadata), "hc", "hc-expanded-v1", metric, unit, amount, time,
            nativeMetric, method, nativeUnit, nativeValue, suffix)

    private fun nutrition(record: NutritionRecord) = listOfNotNull(
        value(record.metadata, "energyIntake", "kcal", record.energy?.inKilocalories, record.endTime, "NutritionRecord.energy"),
        value(record.metadata, "carbohydrateIntake", "g", record.totalCarbohydrate?.inGrams, record.endTime, "NutritionRecord.totalCarbohydrate"),
        value(record.metadata, "proteinIntake", "g", record.protein?.inGrams, record.endTime, "NutritionRecord.protein"),
        value(record.metadata, "fatIntake", "g", record.totalFat?.inGrams, record.endTime, "NutritionRecord.totalFat"),
        value(record.metadata, "sodiumIntake", "mg", record.sodium?.inMilligrams, record.endTime, "NutritionRecord.sodium"),
        value(record.metadata, "caffeineIntake", "mg", record.caffeine?.inMilligrams, record.endTime, "NutritionRecord.caffeine"),
        value(record.metadata, "fiberIntake", "g", record.dietaryFiber?.inGrams, record.endTime, "NutritionRecord.dietaryFiber"),
        value(record.metadata, "sugarIntake", "g", record.sugar?.inGrams, record.endTime, "NutritionRecord.sugar"),
        value(record.metadata, "saturatedFatIntake", "g", record.saturatedFat?.inGrams, record.endTime, "NutritionRecord.saturatedFat"),
    )

    private suspend fun extended(range: TimeRangeFilter, metrics: Set<ScienceMetric>): List<ScienceObservation> =
        buildList {
            if (ScienceMetric.HEART_RATE in metrics) all(HeartRateRecord::class, range).flatMapTo(this) {
                it.samples.mapNotNull { sample -> value(it.metadata, "heartRate", "bpm", sample.beatsPerMinute.toDouble(), sample.time, "HeartRateRecord.samples", suffix = ":" + sample.time.toString()) }
            }
            if (ScienceMetric.WEIGHT in metrics) all(WeightRecord::class, range).mapNotNullTo(this) {
                value(it.metadata, "weight", "kg", it.weight.inKilograms, it.time, "WeightRecord.weight")
            }
            if (ScienceMetric.BODY_FAT in metrics) all(BodyFatRecord::class, range).mapNotNullTo(this) {
                value(it.metadata, "fatPercentage", "%", it.percentage.value, it.time, "BodyFatRecord.percentage", ScienceMethod.PROVIDER_ESTIMATED)
            }
            if (ScienceMetric.LEAN_MASS in metrics) all(LeanBodyMassRecord::class, range).mapNotNullTo(this) {
                value(it.metadata, "fatFreeMass", "kg", it.mass.inKilograms, it.time, "LeanBodyMassRecord.mass", ScienceMethod.PROVIDER_ESTIMATED)
            }
            if (ScienceMetric.BODY_WATER in metrics) all(BodyWaterMassRecord::class, range).mapNotNullTo(this) {
                value(it.metadata, "bodyWater", "kg", it.mass.inKilograms, it.time, "BodyWaterMassRecord.mass", ScienceMethod.PROVIDER_ESTIMATED)
            }
            if (ScienceMetric.BONE_MASS in metrics) all(BoneMassRecord::class, range).mapNotNullTo(this) {
                value(it.metadata, "boneMass", "kg", it.mass.inKilograms, it.time, "BoneMassRecord.mass", ScienceMethod.PROVIDER_ESTIMATED)
            }
            if (ScienceMetric.BMR in metrics) all(BasalMetabolicRateRecord::class, range).mapNotNullTo(this) {
                value(it.metadata, "basalMetabolicRate", "kcal/d", it.basalMetabolicRate.inKilocaloriesPerDay, it.time, "BasalMetabolicRateRecord.basalMetabolicRate", ScienceMethod.PROVIDER_ESTIMATED)
            }
            if (ScienceMetric.HEIGHT in metrics) all(HeightRecord::class, range).mapNotNullTo(this) {
                value(it.metadata, "height", "cm", it.height.inMeters * 100, it.time, "HeightRecord.height", nativeUnit = "m", nativeValue = it.height.inMeters)
            }
            if (ScienceMetric.BLOOD_PRESSURE in metrics) all(BloodPressureRecord::class, range).flatMapTo(this) {
                listOfNotNull(value(it.metadata, "systolicBloodPressure", "mmHg", it.systolic.inMillimetersOfMercury, it.time, "BloodPressureRecord.systolic"), value(it.metadata, "diastolicBloodPressure", "mmHg", it.diastolic.inMillimetersOfMercury, it.time, "BloodPressureRecord.diastolic"))
            }
            if (ScienceMetric.BODY_TEMPERATURE in metrics) all(BodyTemperatureRecord::class, range).mapNotNullTo(this) {
                value(it.metadata, "bodyTemperature", "Cel", it.temperature.inCelsius, it.time, "BodyTemperatureRecord.temperature")
            }
            if (ScienceMetric.BLOOD_GLUCOSE in metrics) all(BloodGlucoseRecord::class, range).mapNotNullTo(this) {
                value(it.metadata, "bloodGlucose", "mmol/L", it.level.inMillimolesPerLiter, it.time, "BloodGlucoseRecord.level")
            }
            if (ScienceMetric.STEPS in metrics) all(StepsRecord::class, range).mapNotNullTo(this) {
                value(it.metadata, "steps", "1", it.count.toDouble(), it.endTime, "StepsRecord.count")
            }
            if (ScienceMetric.DISTANCE in metrics) all(DistanceRecord::class, range).mapNotNullTo(this) {
                value(it.metadata, "distance", "m", it.distance.inMeters, it.endTime, "DistanceRecord.distance")
            }
            if (ScienceMetric.FLOORS in metrics) all(FloorsClimbedRecord::class, range).mapNotNullTo(this) {
                value(it.metadata, "floorsClimbed", "1", it.floors, it.endTime, "FloorsClimbedRecord.floors")
            }
            if (ScienceMetric.TOTAL_ENERGY in metrics) all(TotalCaloriesBurnedRecord::class, range).mapNotNullTo(this) {
                value(it.metadata, "energyExpenditure", "kcal", it.energy.inKilocalories, it.endTime, "TotalCaloriesBurnedRecord.energy", ScienceMethod.PROVIDER_ESTIMATED)
            }
            if (ScienceMetric.ACTIVE_ENERGY in metrics) all(ActiveCaloriesBurnedRecord::class, range).mapNotNullTo(this) {
                value(it.metadata, "activeEnergyExpenditure", "kcal", it.energy.inKilocalories, it.endTime, "ActiveCaloriesBurnedRecord.energy", ScienceMethod.PROVIDER_ESTIMATED)
            }
            if (ScienceMetric.NUTRITION in metrics) all(NutritionRecord::class, range).flatMapTo(this) {
                nutrition(it)
            }
            if (ScienceMetric.HYDRATION in metrics) all(HydrationRecord::class, range).mapNotNullTo(this) {
                value(it.metadata, "fluidIntake", "mL", it.volume.inLiters * 1000, it.endTime, "HydrationRecord.volume", nativeUnit = "L", nativeValue = it.volume.inLiters)
            }
        }

    private suspend fun <T : Record> all(type: KClass<T>, range: TimeRangeFilter): List<T> {
        val records = mutableListOf<T>()
        var token: String? = null
        do {
            val page = client.readRecords(ReadRecordsRequest(type, range, pageSize = PAGE_SIZE, pageToken = token))
            records += page.records
            token = page.pageToken?.takeIf { it.isNotEmpty() }
        } while (token != null && records.size < MAX_RECORDS)
        // Do not silently truncate a window and then advance the checkpoint beyond unread records.
        check(token == null && records.size <= MAX_RECORDS) { "Finestra Health Connect troppo densa" }
        return records
    }

    private fun origin(metadata: Metadata) = RecordOrigin(metadata.id, metadata.lastModifiedTime, metadata.dataOrigin.packageName)

    companion object {
        private const val PAGE_SIZE = 1000

        /** Bound per type and window, so a window of dense SpO2 readings never exhausts memory. */
        private const val MAX_RECORDS = 20_000

        private val TYPES: Map<ScienceMetric, KClass<out Record>> = mapOf(
            ScienceMetric.RESTING_HEART_RATE to RestingHeartRateRecord::class,
            ScienceMetric.HRV_RMSSD to HeartRateVariabilityRmssdRecord::class,
            ScienceMetric.SLEEP to SleepSessionRecord::class,
            ScienceMetric.OXYGEN_SATURATION to OxygenSaturationRecord::class,
            ScienceMetric.RESPIRATORY_RATE to RespiratoryRateRecord::class,
            ScienceMetric.VO2_MAX to Vo2MaxRecord::class,
            ScienceMetric.HEART_RATE to HeartRateRecord::class,
            ScienceMetric.WEIGHT to WeightRecord::class,
            ScienceMetric.BODY_FAT to BodyFatRecord::class,
            ScienceMetric.LEAN_MASS to LeanBodyMassRecord::class,
            ScienceMetric.BODY_WATER to BodyWaterMassRecord::class,
            ScienceMetric.BONE_MASS to BoneMassRecord::class,
            ScienceMetric.BMR to BasalMetabolicRateRecord::class,
            ScienceMetric.HEIGHT to HeightRecord::class,
            ScienceMetric.BLOOD_PRESSURE to BloodPressureRecord::class,
            ScienceMetric.BODY_TEMPERATURE to BodyTemperatureRecord::class,
            ScienceMetric.BLOOD_GLUCOSE to BloodGlucoseRecord::class,
            ScienceMetric.STEPS to StepsRecord::class,
            ScienceMetric.DISTANCE to DistanceRecord::class,
            ScienceMetric.FLOORS to FloorsClimbedRecord::class,
            ScienceMetric.TOTAL_ENERGY to TotalCaloriesBurnedRecord::class,
            ScienceMetric.ACTIVE_ENERGY to ActiveCaloriesBurnedRecord::class,
            ScienceMetric.NUTRITION to NutritionRecord::class,
            ScienceMetric.HYDRATION to HydrationRecord::class,
        )

        /** Read permissions of the scientific part; all optional. */
        val permissions: Set<String> = TYPES.values.map { HealthPermission.getReadPermission(it) }.toSet()

        /** Metrics whose permission is in [granted]. */
        fun grantedMetrics(granted: Set<String>): Set<ScienceMetric> =
            TYPES.filterValues { HealthPermission.getReadPermission(it) in granted }.keys
    }
}
