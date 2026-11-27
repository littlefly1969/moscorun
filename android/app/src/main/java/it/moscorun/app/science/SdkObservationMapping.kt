package it.moscorun.app.science

import java.time.Instant

/** Maps only values actually exposed by an SDK. Native unit and field are retained; no missing-value substitution. */
object SdkObservationMapping {
    fun value(origin: RecordOrigin, prefix: String, version: String, metric: String, unit: String,
        value: Double?, time: Instant, nativeMetric: String, method: ScienceMethod = ScienceMethod.PROVIDER_REPORTED,
        nativeUnit: String = unit, nativeValue: Double? = value, suffix: String = ""): ScienceObservation? {
        if (origin.id.isBlank() || value == null || !value.isFinite()) return null
        val identity = "$prefix:${origin.id}$suffix"
        if (identity.length > 512) return null
        return ScienceObservation(identity, origin.lastModified.toEpochMilli().coerceAtLeast(0), metric, unit,
            value, time, method, origin.app.take(200).ifBlank { null }, nativeMetric, nativeUnit,
            nativeValue?.takeIf { it.isFinite() }, version)
    }
    /**
     * SDK aggregate results have interval bounds and a value, not a native revision timestamp.
     * Keep changed values as unordered revisions; never invent an update time or overwrite a previous result.
     */
    fun samsungDailyAggregate(metric: String, unit: String, value: Double?, start: Instant, end: Instant,
        nativeMetric: String, method: ScienceMethod): ScienceObservation? {
        if (value == null || !value.isFinite() || value < 0 || !end.isAfter(start)) return null
        val origin = RecordOrigin("daily:$start:$end", start, "com.sec.android.app.shealth:aggregate")
        return value(origin, "sh-aggregate", "samsung-aggregate-v1", metric, unit, value, end.minusMillis(1),
            nativeMetric, method)?.copy(externalRevision = "value:" + java.lang.Double.toHexString(value), revisionOrder = null)
    }
}
