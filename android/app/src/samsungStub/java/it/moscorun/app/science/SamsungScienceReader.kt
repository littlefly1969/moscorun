package it.moscorun.app.science

import android.app.Activity
import android.content.Context
import java.time.Instant

/** Public CI has no proprietary AAR. This code is never packaged in an official APK. */
class SamsungScienceReader(@Suppress("UNUSED_PARAMETER") context: Context) : ScienceReader {
    override val source = "samsung-health"
    override val supportedMetrics get() = ScienceCapabilities.samsung
    override suspend fun availability() = ScienceAvailability(emptySet(), message = "Samsung Health Data SDK assente dalla build di test.", permissionsKnown = false)
    suspend fun requestPermissions(@Suppress("UNUSED_PARAMETER") activity: Activity) = availability()
    override suspend fun read(from: Instant, to: Instant, metrics: Set<ScienceMetric>): List<ScienceObservation> =
        error("Samsung Health Data SDK assente")
}
