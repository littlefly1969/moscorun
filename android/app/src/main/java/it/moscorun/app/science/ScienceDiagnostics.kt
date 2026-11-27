package it.moscorun.app.science

import com.google.gson.GsonBuilder
import it.moscorun.app.server.WireFormat
import java.time.Instant
import java.util.UUID

/** Per-run counters of mapped observations, not raw SDK records, sensor capabilities or unique stored revisions. */
@WireFormat
data class ScienceMetricDiagnostic(val metric: String, val permission: String, val read: Int,
    val attempted: Int, val confirmed: Int, val rejected: Int, val conflicts: Int)

@WireFormat
data class ScienceDiagnosticReport(val source: String, val runId: String, val startedAt: String,
    val finishedAt: String, val state: String, val firstReadFrom: String?, val readThrough: String?,
    val windowsRead: Int, val metrics: List<ScienceMetricDiagnostic>) {
    fun json(): String = GsonBuilder().serializeNulls().create().toJson(this)

    fun describe(): String = metrics.joinToString("; ") { m ->
        "${m.metric}: ${m.permission}, letti ${m.read}, tentati ${m.attempted}, confermati ${m.confirmed}, rifiutati ${m.rejected} (conflitti ${m.conflicts})"
    }
}

internal class ScienceDiagnostics(private val source: String, private val started: Instant) {
    private val runId = UUID.randomUUID().toString()
    private data class Counter(var read: Int = 0, var attempted: Int = 0, var confirmed: Int = 0,
        var rejected: Int = 0, var conflicts: Int = 0)

    private val keys = linkedMapOf(
        "restingHeartRate" to ScienceMetric.RESTING_HEART_RATE,
        "hrvRmssd" to ScienceMetric.HRV_RMSSD,
        "sleepDuration" to ScienceMetric.SLEEP,
        "sleepInBedDuration" to ScienceMetric.SLEEP,
        "spo2" to ScienceMetric.OXYGEN_SATURATION,
        "respirationRate" to ScienceMetric.RESPIRATORY_RATE,
        "vo2max" to ScienceMetric.VO2_MAX,
    )
    private val counters = keys.keys.associateWith { Counter() }
    private var availability: ScienceAvailability? = null
    private var first: Instant? = null
    private var through: Instant? = null
    private var windows = 0

    fun permissions(value: ScienceAvailability) { availability = value }

    fun read(from: Instant, to: Instant, items: List<ScienceObservation>) {
        if (first == null) first = from
        through = to
        windows++
        items.forEach { counters[it.metric]?.let { c -> c.read++ } }
    }

    fun attempted(items: List<ScienceObservation>) { items.forEach { counters[it.metric]?.let { c -> c.attempted++ } } }

    fun confirmed(items: List<ScienceObservation>) { items.forEach { counters[it.metric]?.let { c -> c.confirmed++ } } }

    fun rejected(item: ScienceObservation, conflict: Boolean) {
        counters[item.metric]?.let { c ->
            c.rejected++
            if (conflict) c.conflicts++
        }
    }

    fun report(state: ScienceState, finished: Instant): ScienceDiagnosticReport =
        ScienceDiagnosticReport(source, runId, started.toString(), maxOf(started, finished).toString(), state.name,
            first?.toString(), through?.toString(), windows, keys.map { (metric, permission) ->
                val c = counters.getValue(metric)
                val access = availability
                val status = when {
                    access == null || !access.permissionsKnown -> "unknown"
                    permission in access.metrics -> "granted"
                    else -> "denied"
                }
                ScienceMetricDiagnostic(metric, status, c.read, c.attempted, c.confirmed, c.rejected, c.conflicts)
            })
}
