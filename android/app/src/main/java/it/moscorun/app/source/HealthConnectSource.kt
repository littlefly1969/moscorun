package it.moscorun.app.source

import android.content.Context
import androidx.health.connect.client.HealthConnectClient
import androidx.health.connect.client.aggregate.AggregateMetric
import androidx.health.connect.client.HealthConnectFeatures
import androidx.health.connect.client.permission.HealthPermission
import androidx.health.connect.client.records.DistanceRecord
import androidx.health.connect.client.records.ExerciseRouteResult
import androidx.health.connect.client.records.ExerciseSessionRecord
import androidx.health.connect.client.records.HeartRateRecord
import androidx.health.connect.client.records.PowerRecord
import androidx.health.connect.client.records.Record
import androidx.health.connect.client.records.SpeedRecord
import androidx.health.connect.client.records.StepsCadenceRecord
import androidx.health.connect.client.records.TotalCaloriesBurnedRecord
import androidx.health.connect.client.request.AggregateRequest
import androidx.health.connect.client.request.ReadRecordsRequest
import androidx.health.connect.client.time.TimeRangeFilter
import it.moscorun.app.model.MetricSample
import it.moscorun.app.model.MetricSeries
import it.moscorun.app.model.Provider
import it.moscorun.app.model.SourceWorkout
import it.moscorun.app.model.TrackPoint
import kotlinx.coroutines.CancellationException
import java.time.Instant
import kotlin.reflect.KClass

/**
 * Workouts read through Health Connect, the alternative to the Samsung SDK inside the same app. It reads exercise
 * sessions with their routes and the series recorded during them, by whatever app wrote them; it never writes.
 */
class HealthConnectSource(context: Context) : WorkoutSource {
    private val appContext = context.applicationContext
    private val client get() = HealthConnectClient.getOrCreate(appContext)

    override val provider = Provider.HEALTH_CONNECT

    override suspend fun status(): SourceStatus = when (HealthConnectClient.getSdkStatus(appContext)) {
        HealthConnectClient.SDK_AVAILABLE -> try {
            val granted = client.permissionController.getGrantedPermissions()
            if (READ_EXERCISE in granted) SourceStatus.Ready
            else SourceStatus.NeedsPermission("Concedi a MoscoRun la lettura degli allenamenti in Health Connect.")
        } catch (e: CancellationException) {
            throw e
        } catch (e: Exception) {
            SourceStatus.Unavailable("Health Connect non risponde (${e.javaClass.simpleName}). Riprova dall'app.")
        }
        HealthConnectClient.SDK_UNAVAILABLE_PROVIDER_UPDATE_REQUIRED ->
            SourceStatus.Unavailable("Health Connect deve essere installato o aggiornato dal Play Store.")
        else -> SourceStatus.Unavailable("Health Connect non è disponibile su questo telefono o profilo.")
    }

    /** What is granted beyond the minimum, for the settings screen: each line is something the user may want to allow. */
    suspend fun hints(): List<String> {
        if (HealthConnectClient.getSdkStatus(appContext) != HealthConnectClient.SDK_AVAILABLE) return emptyList()
        val granted = runCatching { client.permissionController.getGrantedPermissions() }.getOrDefault(emptySet())
        return buildList {
            if (READ_ROUTES !in granted) add("Senza \"Consenti sempre\" per i percorsi la sincronizzazione si ferma al primo allenamento con GPS registrato da un'altra app.")
            if (READ_IN_BACKGROUND !in granted) add("Senza l'accesso in background Health Connect si può leggere solo con MoscoRun aperta.")
            if (READ_HISTORY !in granted) add("Senza l'accesso ai dati precedenti Health Connect mostra solo gli ultimi 30 giorni prima del primo consenso.")
            SERIES.filter { HealthPermission.getReadPermission(it.type) !in granted }.takeIf { it.isNotEmpty() }?.let { missing ->
                add("Non concesse: ${missing.joinToString(", ") { it.label }}. Gli allenamenti arriveranno senza queste serie.")
            }
        }
    }

    /**
     * Permissions worth asking for on this phone: everything already granted is left out, and the ones for
     * background and older data only where this version of Health Connect offers them.
     */
    suspend fun requestable(): Set<String> {
        if (HealthConnectClient.getSdkStatus(appContext) != HealthConnectClient.SDK_AVAILABLE) return emptySet()
        return try {
            val features = client.features
            fun offered(feature: Int) = features.getFeatureStatus(feature) == HealthConnectFeatures.FEATURE_STATUS_AVAILABLE
            val unsupported = buildSet {
                if (!offered(HealthConnectFeatures.FEATURE_READ_HEALTH_DATA_IN_BACKGROUND)) add(READ_IN_BACKGROUND)
                if (!offered(HealthConnectFeatures.FEATURE_READ_HEALTH_DATA_HISTORY)) add(READ_HISTORY)
            }
            permissions - unsupported - client.permissionController.getGrantedPermissions()
        } catch (e: CancellationException) {
            throw e
        } catch (_: Exception) {
            setOf(READ_EXERCISE)
        }
    }

    override suspend fun read(start: Instant, end: Instant, pageToken: String?, pageSize: Int): SourcePage = guarded {
        val granted = client.permissionController.getGrantedPermissions()
        val page = client.readRecords(ReadRecordsRequest(ExerciseSessionRecord::class, TimeRangeFilter.between(start, end),
            ascendingOrder = true, pageSize = pageSize, pageToken = pageToken))
        SourcePage(page.records.map { workout(it, granted) }, page.pageToken?.takeIf { it.isNotEmpty() })
    }

    private suspend fun workout(session: ExerciseSessionRecord, granted: Set<String>): SourceWorkout {
        val route = when (val result = session.exerciseRouteResult) {
            is ExerciseRouteResult.Data -> result.exerciseRoute.route.map {
                TrackPoint(it.time.toString(), it.latitude, it.longitude, it.altitude?.inMeters, it.horizontalAccuracy?.inMeters)
            }
            // Sending the workout now without its route would leave the server with an incomplete copy that the
            // route could never be added to: the run stops and asks for the permission instead.
            is ExerciseRouteResult.ConsentRequired -> throw SourceBlockedException(
                "Health Connect richiede il consenso per i percorsi: concedi \"Consenti sempre\" ai percorsi nelle impostazioni dell'app.")
            else -> emptyList()
        }
        val range = TimeRangeFilter.between(session.startTime, session.endTime)
        val origin = setOf(session.metadata.dataOrigin)
        val series = SERIES.filter { HealthPermission.getReadPermission(it.type) in granted }.map { spec ->
            MetricSeries(spec.metric, samples(spec, range, origin).filter {
                val time = Instant.parse(it.time)
                !time.isBefore(session.startTime) && time.isBefore(session.endTime)
            })
        }
        val totals = totals(range, origin, granted)
        return SourceWorkout(
            provider = Provider.HEALTH_CONNECT,
            sourceId = session.metadata.id,
            revision = session.metadata.lastModifiedTime.toEpochMilli(),
            activityType = HealthConnectMapping.activityType(session.exerciseType),
            start = session.startTime,
            end = session.endTime,
            utcOffsetMinutes = session.startZoneOffset?.totalSeconds?.div(60),
            activeDurationSeconds = totals.activeSeconds,
            distanceM = totals.distanceM,
            energyKcal = totals.energyKcal,
            averageHeartRate = totals.averageHeartRate,
            maxHeartRate = totals.maxHeartRate,
            route = route,
            series = series)
    }

    private suspend fun samples(spec: SeriesSpec, range: TimeRangeFilter, origin: Set<androidx.health.connect.client.records.metadata.DataOrigin>): List<MetricSample> {
        val samples = mutableListOf<MetricSample>()
        var token: String? = null
        do {
            val page = client.readRecords(ReadRecordsRequest(spec.type, range, dataOriginFilter = origin, pageSize = 1000, pageToken = token))
            page.records.forEach { samples += spec.samples(it) }
            token = page.pageToken?.takeIf { it.isNotEmpty() }
        } while (token != null && samples.size < MAX_SAMPLES)
        return samples.take(MAX_SAMPLES)
    }

    private class Totals(val activeSeconds: Double? = null, val distanceM: Double? = null, val energyKcal: Double? = null,
        val averageHeartRate: Double? = null, val maxHeartRate: Double? = null)

    /** Totals over the session interval for the app that recorded it; a total that cannot be computed stays missing. */
    private suspend fun totals(range: TimeRangeFilter, origin: Set<androidx.health.connect.client.records.metadata.DataOrigin>, granted: Set<String>): Totals {
        fun allowed(type: KClass<out Record>) = HealthPermission.getReadPermission(type) in granted
        val metrics = buildSet<AggregateMetric<*>> {
            add(ExerciseSessionRecord.EXERCISE_DURATION_TOTAL)
            if (allowed(DistanceRecord::class)) add(DistanceRecord.DISTANCE_TOTAL)
            if (allowed(TotalCaloriesBurnedRecord::class)) add(TotalCaloriesBurnedRecord.ENERGY_TOTAL)
            if (allowed(HeartRateRecord::class)) { add(HeartRateRecord.BPM_AVG); add(HeartRateRecord.BPM_MAX) }
        }
        return try {
            val result = client.aggregate(AggregateRequest(metrics, range, origin))
            Totals(
                result[ExerciseSessionRecord.EXERCISE_DURATION_TOTAL]?.toMillis()?.div(1000.0),
                result[DistanceRecord.DISTANCE_TOTAL]?.inMeters,
                result[TotalCaloriesBurnedRecord.ENERGY_TOTAL]?.inKilocalories,
                result[HeartRateRecord.BPM_AVG]?.toDouble(),
                result[HeartRateRecord.BPM_MAX]?.toDouble())
        } catch (e: CancellationException) {
            throw e
        } catch (e: SecurityException) {
            throw e
        } catch (_: Exception) {
            Totals()
        }
    }

    private suspend fun <T> guarded(block: suspend () -> T): T = try {
        block()
    } catch (e: CancellationException) {
        throw e
    } catch (e: SourceBlockedException) {
        throw e
    } catch (_: SecurityException) {
        throw SourceBlockedException("Health Connect non consente questa lettura ora: apri MoscoRun per sincronizzare, " +
            "oppure concedi l'accesso in background e ai dati precedenti nelle impostazioni dell'app.")
    } catch (e: Exception) {
        throw SourceBlockedException("Health Connect non ha risposto (${e.javaClass.simpleName}). Riprova dall'app.")
    }

    private class SeriesSpec(val metric: String, val label: String, val type: KClass<out Record>, val samples: (Record) -> List<MetricSample>)

    companion object {
        /** Server limit of samples per series. */
        private const val MAX_SAMPLES = 100_000

        // Declared as literals: they are plain Android permission names, stable across library versions.
        const val READ_ROUTES = "android.permission.health.READ_EXERCISE_ROUTES"
        const val READ_IN_BACKGROUND = "android.permission.health.READ_HEALTH_DATA_IN_BACKGROUND"
        const val READ_HISTORY = "android.permission.health.READ_HEALTH_DATA_HISTORY"
        val READ_EXERCISE: String = HealthPermission.getReadPermission(ExerciseSessionRecord::class)

        private val SERIES = listOf(
            SeriesSpec("HeartRate", "frequenza cardiaca", HeartRateRecord::class) { r ->
                (r as HeartRateRecord).samples.map { MetricSample(it.time.toString(), it.beatsPerMinute.toDouble()) }
            },
            SeriesSpec("Speed", "velocità", SpeedRecord::class) { r ->
                (r as SpeedRecord).samples.map { MetricSample(it.time.toString(), it.speed.inMetersPerSecond) }
            },
            SeriesSpec("Cadence", "cadenza", StepsCadenceRecord::class) { r ->
                (r as StepsCadenceRecord).samples.map { MetricSample(it.time.toString(), it.rate) }
            },
            SeriesSpec("Power", "potenza", PowerRecord::class) { r ->
                (r as PowerRecord).samples.map { MetricSample(it.time.toString(), it.power.inWatts) }
            })

        /** Everything the app may ask Health Connect for; only the first one is required to synchronise. */
        val permissions: Set<String> = setOf(READ_EXERCISE, READ_ROUTES, READ_IN_BACKGROUND, READ_HISTORY) +
            SERIES.map { HealthPermission.getReadPermission(it.type) } +
            listOf(DistanceRecord::class, TotalCaloriesBurnedRecord::class).map { HealthPermission.getReadPermission(it) }
    }
}

/** Activity codes shared with the Samsung source and the dashboard for the common sports; the others keep the Health Connect number. */
internal object HealthConnectMapping {
    private val known = mapOf(
        ExerciseSessionRecord.EXERCISE_TYPE_RUNNING to "RUNNING",
        ExerciseSessionRecord.EXERCISE_TYPE_RUNNING_TREADMILL to "TREADMILL",
        ExerciseSessionRecord.EXERCISE_TYPE_WALKING to "WALKING",
        ExerciseSessionRecord.EXERCISE_TYPE_HIKING to "HIKING",
        ExerciseSessionRecord.EXERCISE_TYPE_BIKING to "CYCLING",
        ExerciseSessionRecord.EXERCISE_TYPE_BIKING_STATIONARY to "CYCLING",
        ExerciseSessionRecord.EXERCISE_TYPE_SWIMMING_POOL to "SWIMMING",
        ExerciseSessionRecord.EXERCISE_TYPE_SWIMMING_OPEN_WATER to "SWIMMING")

    fun activityType(exerciseType: Int): String = known[exerciseType] ?: "HC_$exerciseType"
}
