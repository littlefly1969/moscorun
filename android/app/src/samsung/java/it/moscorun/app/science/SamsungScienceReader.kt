package it.moscorun.app.science

import android.app.Activity
import android.content.Context
import com.samsung.android.sdk.health.data.HealthDataService
import com.samsung.android.sdk.health.data.data.HealthDataPoint
import com.samsung.android.sdk.health.data.permission.AccessType
import com.samsung.android.sdk.health.data.permission.Permission
import com.samsung.android.sdk.health.data.request.AggregateRequest
import com.samsung.android.sdk.health.data.request.DataType
import com.samsung.android.sdk.health.data.request.DataTypes
import com.samsung.android.sdk.health.data.request.InstantTimeFilter
import com.samsung.android.sdk.health.data.request.LocalDateFilter
import com.samsung.android.sdk.health.data.request.LocalTimeFilter
import com.samsung.android.sdk.health.data.request.LocalTimeGroup
import com.samsung.android.sdk.health.data.request.LocalTimeGroupUnit
import com.samsung.android.sdk.health.data.request.Ordering
import com.samsung.android.sdk.health.data.request.ReadDataRequest
import kotlinx.coroutines.CancellationException
import java.time.Duration
import java.time.Instant
import java.time.ZoneOffset

/** Read-only historical connector using the licensed Samsung Health Data SDK 1.1.0 already bundled in the official APK. */
class SamsungScienceReader(context: Context) : ScienceReader {
    private val store by lazy { HealthDataService.getStore(context.applicationContext) }
    override val source = "samsung-health"
    override val isolatedStreams = true
    override val supportedMetrics get() = ScienceCapabilities.samsung
    override fun checkpoint(availability: ScienceAvailability) = ScienceCapabilities.checkpoint(source, "samsung-data-v1", availability)

    private val permissions get() = mapOf(
        ScienceMetric.SAMSUNG_BODY to Permission.of(DataTypes.BODY_COMPOSITION, AccessType.READ),
        ScienceMetric.SAMSUNG_HEART to Permission.of(DataTypes.HEART_RATE, AccessType.READ),
        ScienceMetric.SAMSUNG_OXYGEN to Permission.of(DataTypes.BLOOD_OXYGEN, AccessType.READ),
        ScienceMetric.SAMSUNG_SKIN to Permission.of(DataTypes.SKIN_TEMPERATURE, AccessType.READ),
        ScienceMetric.SAMSUNG_SLEEP to Permission.of(DataTypes.SLEEP, AccessType.READ),
        ScienceMetric.SAMSUNG_PRESSURE to Permission.of(DataTypes.BLOOD_PRESSURE, AccessType.READ),
        ScienceMetric.SAMSUNG_NUTRITION to Permission.of(DataTypes.NUTRITION, AccessType.READ),
        ScienceMetric.SAMSUNG_WATER to Permission.of(DataTypes.WATER_INTAKE, AccessType.READ),
        ScienceMetric.SAMSUNG_GLUCOSE to Permission.of(DataTypes.BLOOD_GLUCOSE, AccessType.READ),
        ScienceMetric.SAMSUNG_TEMP to Permission.of(DataTypes.BODY_TEMPERATURE, AccessType.READ),
        ScienceMetric.SAMSUNG_FLOORS to Permission.of(DataTypes.FLOORS_CLIMBED, AccessType.READ),
        ScienceMetric.SAMSUNG_ENERGY to Permission.of(DataTypes.ENERGY_SCORE, AccessType.READ),
        ScienceMetric.SAMSUNG_STEPS to Permission.of(DataTypes.STEPS, AccessType.READ),
        ScienceMetric.SAMSUNG_ACTIVITY to Permission.of(DataTypes.ACTIVITY_SUMMARY, AccessType.READ),
    )

    override suspend fun availability(): ScienceAvailability = try {
        val requested = permissions
        val granted = store.getGrantedPermissions(requested.values.toSet())
        ScienceAvailability(requested.filterValues { it in granted }.keys, history = true,
            message = if (granted.isEmpty()) "Concedi a MoscoRun i permessi scientifici di Samsung Health." else null)
    } catch (e: CancellationException) {
        throw e
    } catch (e: Exception) {
        ScienceAvailability(emptySet(), message = "Samsung Health Data SDK non disponibile (${e.javaClass.simpleName}).", permissionsKnown = false)
    }

    suspend fun requestPermissions(activity: Activity): ScienceAvailability {
        try {
            store.requestPermissions(permissions.values.toSet(), activity)
        } catch (e: CancellationException) {
            throw e
        } catch (e: Exception) {
            return ScienceAvailability(emptySet(), message = "Samsung Health non ha concesso l'accesso (${e.javaClass.simpleName}).", permissionsKnown = false)
        }
        return availability()
    }

    override suspend fun read(from: Instant, to: Instant, metrics: Set<ScienceMetric>): List<ScienceObservation> =
        buildList {
            if (ScienceMetric.SAMSUNG_BODY in metrics) for (point in all(DataTypes.BODY_COMPOSITION.readDataRequestBuilder, from, to)) {
                    addValue(point, "weight", "kg", point.getValue(DataType.BodyCompositionType.WEIGHT)?.toDouble(), "BodyCompositionType.WEIGHT")
                    addValue(point, "height", "cm", point.getValue(DataType.BodyCompositionType.HEIGHT)?.toDouble(), "BodyCompositionType.HEIGHT")
                    addValue(point, "basalMetabolicRate", "kcal/d", point.getValue(DataType.BodyCompositionType.BASAL_METABOLIC_RATE)?.toDouble(), "BodyCompositionType.BASAL_METABOLIC_RATE", method = ScienceMethod.PROVIDER_ESTIMATED)
                    addValue(point, "fatPercentage", "%", point.getValue(DataType.BodyCompositionType.BODY_FAT)?.toDouble(), "BodyCompositionType.BODY_FAT", method = ScienceMethod.PROVIDER_ESTIMATED)
                    addValue(point, "fatMass", "kg", point.getValue(DataType.BodyCompositionType.BODY_FAT_MASS)?.toDouble(), "BodyCompositionType.BODY_FAT_MASS", method = ScienceMethod.PROVIDER_ESTIMATED)
                    addValue(point, "fatFreePercentage", "%", point.getValue(DataType.BodyCompositionType.FAT_FREE)?.toDouble(), "BodyCompositionType.FAT_FREE", method = ScienceMethod.PROVIDER_ESTIMATED)
                    addValue(point, "fatFreeMass", "kg", point.getValue(DataType.BodyCompositionType.FAT_FREE_MASS)?.toDouble(), "BodyCompositionType.FAT_FREE_MASS", method = ScienceMethod.PROVIDER_ESTIMATED)
                    addValue(point, "musclePercentage", "%", point.getValue(DataType.BodyCompositionType.MUSCLE_MASS)?.toDouble(), "BodyCompositionType.MUSCLE_MASS", method = ScienceMethod.PROVIDER_ESTIMATED)
                    addValue(point, "skeletalMusclePercentage", "%", point.getValue(DataType.BodyCompositionType.SKELETAL_MUSCLE)?.toDouble(), "BodyCompositionType.SKELETAL_MUSCLE", method = ScienceMethod.PROVIDER_ESTIMATED)
                    addValue(point, "skeletalMuscleMass", "kg", point.getValue(DataType.BodyCompositionType.SKELETAL_MUSCLE_MASS)?.toDouble(), "BodyCompositionType.SKELETAL_MUSCLE_MASS", method = ScienceMethod.PROVIDER_ESTIMATED)
                    addValue(point, "bodyWaterVolume", "L", point.getValue(DataType.BodyCompositionType.TOTAL_BODY_WATER)?.toDouble(), "BodyCompositionType.TOTAL_BODY_WATER", method = ScienceMethod.PROVIDER_ESTIMATED)
                    addValue(point, "bodyMassIndex", "kg/m2", point.getValue(DataType.BodyCompositionType.BODY_MASS_INDEX)?.toDouble(), "BodyCompositionType.BODY_MASS_INDEX", method = ScienceMethod.PROVIDER_ESTIMATED)
            }
            if (ScienceMetric.SAMSUNG_HEART in metrics) for (point in all(DataTypes.HEART_RATE.readDataRequestBuilder, from, to)) {
                    val samples = point.getValue(DataType.HeartRateType.SERIES_DATA).orEmpty()
                    if (samples.isEmpty()) {
                        addValue(point, "heartRate", "bpm", point.getValue(DataType.HeartRateType.HEART_RATE)?.toDouble(),
                            "HeartRateType.HEART_RATE")
                    } else {
                        for (sample in samples) addValue(point, "heartRate", "bpm", sample.heartRate.toDouble(),
                            "HeartRateType.SERIES_DATA", time = sample.startTime, suffix = ":series:" + sample.startTime)
                    }
            }
            if (ScienceMetric.SAMSUNG_OXYGEN in metrics) for (point in all(DataTypes.BLOOD_OXYGEN.readDataRequestBuilder, from, to)) {
                    val samples = point.getValue(DataType.BloodOxygenType.SERIES_DATA).orEmpty()
                    if (samples.isEmpty()) {
                        addValue(point, "spo2", "%", point.getValue(DataType.BloodOxygenType.OXYGEN_SATURATION)?.toDouble(),
                            "BloodOxygenType.OXYGEN_SATURATION")
                    } else {
                        for (sample in samples) addValue(point, "spo2", "%", sample.oxygenSaturation.toDouble(),
                            "BloodOxygenType.SERIES_DATA", time = sample.startTime, suffix = ":series:" + sample.startTime)
                    }
            }
            if (ScienceMetric.SAMSUNG_SKIN in metrics) for (point in all(DataTypes.SKIN_TEMPERATURE.readDataRequestBuilder, from, to)) {
                    val samples = point.getValue(DataType.SkinTemperatureType.SERIES_DATA).orEmpty()
                    if (samples.isEmpty()) {
                        addValue(point, "skinTemperature", "Cel", point.getValue(DataType.SkinTemperatureType.SKIN_TEMPERATURE)?.toDouble(),
                            "SkinTemperatureType.SKIN_TEMPERATURE")
                    } else {
                        for (sample in samples) addValue(point, "skinTemperature", "Cel", sample.skinTemperature.toDouble(),
                            "SkinTemperatureType.SERIES_DATA", time = sample.startTime, suffix = ":series:" + sample.startTime)
                    }
            }
            if (ScienceMetric.SAMSUNG_PRESSURE in metrics) for (point in all(DataTypes.BLOOD_PRESSURE.readDataRequestBuilder, from, to)) {
                    addValue(point, "systolicBloodPressure", "mmHg", point.getValue(DataType.BloodPressureType.SYSTOLIC)?.toDouble(), "BloodPressureType.SYSTOLIC")
                    addValue(point, "diastolicBloodPressure", "mmHg", point.getValue(DataType.BloodPressureType.DIASTOLIC)?.toDouble(), "BloodPressureType.DIASTOLIC")
                    addValue(point, "meanBloodPressure", "mmHg", point.getValue(DataType.BloodPressureType.MEAN)?.toDouble(), "BloodPressureType.MEAN")
                    addValue(point, "pulseRate", "bpm", point.getValue(DataType.BloodPressureType.PULSE_RATE)?.toDouble(), "BloodPressureType.PULSE_RATE")
            }
            if (ScienceMetric.SAMSUNG_NUTRITION in metrics) for (point in all(DataTypes.NUTRITION.readDataRequestBuilder, from, to)) {
                    addValue(point, "energyIntake", "kcal", point.getValue(DataType.NutritionType.CALORIES)?.toDouble(), "NutritionType.CALORIES")
                    addValue(point, "carbohydrateIntake", "g", point.getValue(DataType.NutritionType.CARBOHYDRATE)?.toDouble(), "NutritionType.CARBOHYDRATE")
                    addValue(point, "proteinIntake", "g", point.getValue(DataType.NutritionType.PROTEIN)?.toDouble(), "NutritionType.PROTEIN")
                    addValue(point, "fatIntake", "g", point.getValue(DataType.NutritionType.TOTAL_FAT)?.toDouble(), "NutritionType.TOTAL_FAT")
                    addValue(point, "sodiumIntake", "mg", point.getValue(DataType.NutritionType.SODIUM)?.toDouble(), "NutritionType.SODIUM")
                    addValue(point, "fiberIntake", "g", point.getValue(DataType.NutritionType.DIETARY_FIBER)?.toDouble(), "NutritionType.DIETARY_FIBER")
                    addValue(point, "sugarIntake", "g", point.getValue(DataType.NutritionType.SUGAR)?.toDouble(), "NutritionType.SUGAR")
                    addValue(point, "saturatedFatIntake", "g", point.getValue(DataType.NutritionType.SATURATED_FAT)?.toDouble(), "NutritionType.SATURATED_FAT")
            }
            if (ScienceMetric.SAMSUNG_WATER in metrics) for (point in all(DataTypes.WATER_INTAKE.readDataRequestBuilder, from, to)) {
                    addValue(point, "fluidIntake", "mL", point.getValue(DataType.WaterIntakeType.AMOUNT)?.toDouble(), "WaterIntakeType.AMOUNT")
            }
            if (ScienceMetric.SAMSUNG_GLUCOSE in metrics) for (point in all(DataTypes.BLOOD_GLUCOSE.readDataRequestBuilder, from, to)) {
                    addValue(point, "bloodGlucose", "mmol/L", point.getValue(DataType.BloodGlucoseType.GLUCOSE_LEVEL)?.toDouble(), "BloodGlucoseType.GLUCOSE_LEVEL")
            }
            if (ScienceMetric.SAMSUNG_TEMP in metrics) for (point in all(DataTypes.BODY_TEMPERATURE.readDataRequestBuilder, from, to)) {
                    addValue(point, "bodyTemperature", "Cel", point.getValue(DataType.BodyTemperatureType.BODY_TEMPERATURE)?.toDouble(), "BodyTemperatureType.BODY_TEMPERATURE")
            }
            if (ScienceMetric.SAMSUNG_FLOORS in metrics) for (point in all(DataTypes.FLOORS_CLIMBED.readDataRequestBuilder, from, to)) {
                    addValue(point, "floorsClimbed", "1", point.getValue(DataType.FloorsClimbedType.FLOOR)?.toDouble(), "FloorsClimbedType.FLOOR")
            }
            if (ScienceMetric.SAMSUNG_SLEEP in metrics) for (point in all(DataTypes.SLEEP.readDataRequestBuilder, from, to)) {
                    val end = point.endTime ?: point.startTime
                    addValue(point, "sleepScore", "native", point.getValue(DataType.SleepType.SLEEP_SCORE)?.toDouble(),
                        "SleepType.SLEEP_SCORE", time = end, method = ScienceMethod.PROVIDER_ESTIMATED)
                    addValue(point, "sleepDuration", "s", point.getValue(DataType.SleepType.DURATION)?.toMillis()?.div(1000.0),
                        "SleepType.DURATION", time = end)
                    if (end.isAfter(point.startTime)) addValue(point, "sleepInBedDuration", "s",
                        Duration.between(point.startTime, end).toMillis() / 1000.0, "SleepType.interval", time = end)
                    for (session in point.getValue(DataType.SleepType.SESSIONS).orEmpty()) {
                        for (stage in session.stages.orEmpty()) {
                            if (!stage.endTime.isAfter(stage.startTime)) continue
                            addValue(point, "sleepStageDuration", "s",
                                Duration.between(stage.startTime, stage.endTime).toMillis() / 1000.0,
                                "SleepType.stage." + stage.stage.name, time = stage.endTime,
                                suffix = ":stage:" + stage.startTime)
                        }
                    }
            }
            if (ScienceMetric.SAMSUNG_STEPS in metrics) {
                addAll(daily(DataType.StepsType.TOTAL.requestBuilder, from, to, "steps", "1",
                    "StepsType.TOTAL", ScienceMethod.PROVIDER_REPORTED) { it.toDouble() })
            }
            if (ScienceMetric.SAMSUNG_ACTIVITY in metrics) {
                addAll(daily(DataType.ActivitySummaryType.TOTAL_ACTIVE_TIME.requestBuilder, from, to,
                    "activeDuration", "s", "ActivitySummaryType.TOTAL_ACTIVE_TIME", ScienceMethod.PROVIDER_REPORTED) {
                    it.toMillis() / 1000.0
                })
                addAll(daily(DataType.ActivitySummaryType.TOTAL_ACTIVE_CALORIES_BURNED.requestBuilder, from, to,
                    "activeEnergyExpenditure", "kcal", "ActivitySummaryType.TOTAL_ACTIVE_CALORIES_BURNED", ScienceMethod.PROVIDER_ESTIMATED) { it.toDouble() })
                addAll(daily(DataType.ActivitySummaryType.TOTAL_CALORIES_BURNED.requestBuilder, from, to,
                    "energyExpenditure", "kcal", "ActivitySummaryType.TOTAL_CALORIES_BURNED", ScienceMethod.PROVIDER_ESTIMATED) { it.toDouble() })
                addAll(daily(DataType.ActivitySummaryType.TOTAL_DISTANCE.requestBuilder, from, to,
                    "distance", "m", "ActivitySummaryType.TOTAL_DISTANCE", ScienceMethod.PROVIDER_REPORTED) { it.toDouble() })
            }
            if (ScienceMetric.SAMSUNG_ENERGY in metrics) {
                // Local-day records: widen around UTC boundaries, then filter by the exact source timestamp.
                val filter = LocalDateFilter.of(from.atOffset(ZoneOffset.UTC).toLocalDate().minusDays(1),
                    to.atOffset(ZoneOffset.UTC).toLocalDate().plusDays(1))
                val points = pages { token -> store.readData(DataTypes.ENERGY_SCORE.readDataRequestBuilder
                    .setLocalDateFilter(filter).setPageSize(500).setPageToken(token).build()) }
                for (point in points) addValue(point, "energyScore", "native",
                    point.getValue(DataType.EnergyScoreType.ENERGY_SCORE)?.toDouble(), "EnergyScoreType.ENERGY_SCORE",
                    method = ScienceMethod.PROVIDER_ESTIMATED)
            }
        }.filter { !it.time.isBefore(from) && it.time.isBefore(to) }

    /** Use the SDK's deduplicated daily aggregate, not a sum of overlapping phone/watch records. */
    private suspend fun <T : Any> daily(builder: AggregateRequest.LocalTimeBuilder<T>, from: Instant, to: Instant,
        metric: String, unit: String, field: String, method: ScienceMethod,
        convert: (T) -> Double): List<ScienceObservation> {
        // Widen local-date bounds across zone offsets; filter against the exact SDK interval below.
        val localFrom = from.atOffset(ZoneOffset.UTC).toLocalDate().minusDays(2).atStartOfDay()
        val localTo = to.atOffset(ZoneOffset.UTC).toLocalDate().plusDays(2).atStartOfDay()
        val filter = LocalTimeFilter.of(localFrom, localTo)
        val group = LocalTimeGroup.of(LocalTimeGroupUnit.DAILY, 1)
        val result = mutableListOf<ScienceObservation>()
        val seen = mutableSetOf<String>()
        var token: String? = null
        do {
            val page = store.aggregateData(builder.setLocalTimeFilterWithGroup(filter, group)
                .setPageSize(500).setPageToken(token).setOrdering(Ordering.ASC).build())
            for (item in page.dataList) {
                if (item.endTime.isAfter(to) || !item.endTime.isAfter(from)) continue
                SdkObservationMapping.samsungDailyAggregate(metric, unit, item.value?.let(convert),
                    item.startTime, item.endTime, field, method)?.let(result::add)
            }
            token = page.pageToken?.takeIf { it.isNotBlank() }
            check(token == null || seen.add(token)) { "Paginazione aggregati Samsung non valida" }
        } while (token != null)
        return result
    }

    private suspend fun all(builder: ReadDataRequest.DualTimeBuilder<HealthDataPoint>, from: Instant, to: Instant) =
        // Read records that start before a window but contain samples within it (e.g. sleep).
        pages { token -> store.readData(builder.setInstantTimeFilter(InstantTimeFilter.of(from.minus(Duration.ofDays(2)), to))
            .setPageSize(500).setPageToken(token).setOrdering(Ordering.ASC).build()) }

    private suspend fun pages(read: suspend (String?) -> com.samsung.android.sdk.health.data.response.DataResponse<HealthDataPoint>): List<HealthDataPoint> {
        val items = mutableListOf<HealthDataPoint>()
        var token: String? = null
        val seen = mutableSetOf<String>()
        do {
            val result = read(token)
            items += result.dataList
            token = result.pageToken?.takeIf { it.isNotBlank() }
            check(items.size <= 20_000 && (token == null || seen.add(token))) { "Finestra Samsung troppo densa o paginazione non valida" }
        } while (token != null)
        return items
    }

    private fun MutableList<ScienceObservation>.addValue(point: HealthDataPoint, metric: String, unit: String,
        value: Double?, field: String, time: Instant = point.startTime, suffix: String = "",
        method: ScienceMethod = ScienceMethod.PROVIDER_REPORTED) {
        val origin = RecordOrigin(point.uid, point.updateTime ?: point.startTime, point.dataSource?.appId.orEmpty())
        SdkObservationMapping.value(origin, "sh", "samsung-data-v1", metric, unit, value, time, field, method,
            suffix = suffix)?.let(::add)
    }
}
