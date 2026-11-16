package it.moscorun.app.source

import android.app.Activity
import android.content.Context
import com.samsung.android.sdk.health.data.HealthDataService
import com.samsung.android.sdk.health.data.data.HealthDataPoint
import com.samsung.android.sdk.health.data.data.entries.ExerciseLocation
import com.samsung.android.sdk.health.data.data.entries.ExerciseLog
import com.samsung.android.sdk.health.data.data.entries.ExerciseSession
import com.samsung.android.sdk.health.data.permission.AccessType
import com.samsung.android.sdk.health.data.permission.Permission
import com.samsung.android.sdk.health.data.request.DataType
import com.samsung.android.sdk.health.data.request.DataTypes
import com.samsung.android.sdk.health.data.request.InstantTimeFilter
import com.samsung.android.sdk.health.data.request.Ordering
import it.moscorun.app.model.MetricSample
import it.moscorun.app.model.MetricSeries
import it.moscorun.app.model.Provider
import it.moscorun.app.model.SourceWorkout
import it.moscorun.app.model.TrackPoint
import kotlinx.coroutines.CancellationException
import java.time.Instant

/**
 * Workouts read directly from Samsung Health through the official Samsung Health Data SDK (the licensed AAR is
 * supplied privately at build time and is never part of the repository). Only exercise sessions and their routes
 * are requested: nothing else is read, nothing is written.
 *
 * Distribution outside development requires Samsung's partner registration; on a personal phone the SDK developer
 * mode of Samsung Health must be enabled. When Samsung Health refuses a request the source reports why and the
 * synchronisation waits for the user: there is no fallback that bypasses the SDK.
 */
class SamsungHealthSource(context: Context) : WorkoutSource {
    private val appContext = context.applicationContext
    private val store by lazy { HealthDataService.getStore(appContext) }
    private val exercise get() = Permission.of(DataTypes.EXERCISE, AccessType.READ)
    private val route get() = Permission.of(DataTypes.EXERCISE_LOCATION, AccessType.READ)
    private val permissions get() = setOf(exercise, route)

    override val provider = Provider.SAMSUNG_HEALTH

    override suspend fun status(): SourceStatus = try {
        val granted = store.getGrantedPermissions(permissions)
        when {
            exercise !in granted -> SourceStatus.NeedsPermission("Concedi a MoscoRun la lettura degli allenamenti di Samsung Health.")
            route !in granted -> SourceStatus.NeedsPermission("Concedi a MoscoRun anche la lettura dei percorsi degli allenamenti di Samsung Health.")
            else -> SourceStatus.Ready
        }
    } catch (e: CancellationException) {
        throw e
    } catch (e: Exception) {
        SourceStatus.Unavailable(unavailable(e))
    }

    /** Shows Samsung Health's own consent screen. Must be called from a visible activity, never from the background. */
    suspend fun requestPermissions(activity: Activity): SourceStatus = try {
        store.requestPermissions(permissions, activity)
        status()
    } catch (e: CancellationException) {
        throw e
    } catch (e: Exception) {
        SourceStatus.Unavailable(unavailable(e))
    }

    override suspend fun read(start: Instant, end: Instant, pageToken: String?, pageSize: Int): SourcePage {
        val response = try {
            store.readData(DataTypes.EXERCISE.readDataRequestBuilder
                .setInstantTimeFilter(InstantTimeFilter.of(start, end))
                .setPageSize(pageSize).setPageToken(pageToken).setOrdering(Ordering.ASC).build())
        } catch (e: CancellationException) {
            throw e
        } catch (e: Exception) {
            // Samsung Health reports missing permissions, a disabled developer mode and an outdated app this way.
            throw SourceBlockedException(unavailable(e))
        }
        val workouts = response.dataList.flatMap { point ->
            point.getValue(DataType.ExerciseType.SESSIONS).orEmpty().mapIndexed { index, session -> SamsungMapping.workout(point, index, session) }
        }
        return SourcePage(workouts, response.pageToken?.takeIf { it.isNotEmpty() })
    }

    private fun unavailable(e: Exception) =
        "Samsung Health non ha permesso la lettura (${e.javaClass.simpleName}). Verifica che Samsung Health sia aggiornato, " +
            "che la modalità sviluppatore del Samsung Health Data SDK sia attiva e che i permessi siano concessi, poi riprova dall'app."
}

/** Pure conversions from SDK objects, kept apart so they can be tested with real SDK types and no Samsung Health. */
internal object SamsungMapping {
    /**
     * The identity is the Samsung record plus the index of the session inside it, as in every previous version
     * of the app: changing it would make the server see each workout as new. The revision is the record update
     * time, so an edit in Samsung Health replaces the copy on the server.
     */
    fun workout(point: HealthDataPoint, index: Int, session: ExerciseSession): SourceWorkout {
        val logs = session.log.orEmpty()
        // Records are filtered and ordered by their own start, which is where an interrupted reading resumes.
        val recordStart: Instant? = point.startTime
        return SourceWorkout(
            provider = Provider.SAMSUNG_HEALTH,
            sourceId = "${point.uid}:$index",
            revision = point.updateTime?.toEpochMilli() ?: 0L,
            activityType = session.exerciseType.toString(),
            start = session.startTime,
            end = session.endTime,
            utcOffsetMinutes = point.zoneOffset?.totalSeconds?.div(60),
            activeDurationSeconds = session.duration?.toMillis()?.div(1000.0),
            distanceM = session.distance?.toDouble(),
            energyKcal = session.calories?.toDouble(),
            averageHeartRate = session.meanHeartRate?.toDouble(),
            maxHeartRate = session.maxHeartRate?.toDouble(),
            route = route(session.route.orEmpty()),
            series = listOf(
                series("HeartRate", logs) { it.heartRate },
                series("Speed", logs) { it.speed },
                series("Cadence", logs) { it.cadence },
                series("Power", logs) { it.power }),
            cursor = recordStart ?: session.startTime)
    }

    fun route(points: List<ExerciseLocation>): List<TrackPoint> = points.map {
        TrackPoint(it.timestamp.toString(), it.latitude.toDouble(), it.longitude.toDouble(), it.altitude?.toDouble(), it.accuracy?.toDouble())
    }

    private fun series(metric: String, logs: List<ExerciseLog>, value: (ExerciseLog) -> Float?) =
        MetricSeries(metric, logs.mapNotNull { log -> value(log)?.let { MetricSample(log.timestamp.toString(), it.toDouble()) } })
}
