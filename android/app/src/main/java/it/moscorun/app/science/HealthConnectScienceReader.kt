package it.moscorun.app.science

import android.content.Context
import androidx.health.connect.client.HealthConnectClient
import androidx.health.connect.client.permission.HealthPermission
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
        return records.flatMap(HealthConnectScienceMapping::observations)
            .filter { !it.time.isBefore(from) && it.time.isBefore(to) }
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
            ScienceMetric.VO2_MAX to Vo2MaxRecord::class)

        /** Read permissions of the scientific part; all optional. */
        val permissions: Set<String> = TYPES.values.map { HealthPermission.getReadPermission(it) }.toSet()

        /** Metrics whose permission is in [granted]. */
        fun grantedMetrics(granted: Set<String>): Set<ScienceMetric> =
            TYPES.filterValues { HealthPermission.getReadPermission(it) in granted }.keys
    }
}
