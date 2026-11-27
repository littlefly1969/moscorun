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
}
