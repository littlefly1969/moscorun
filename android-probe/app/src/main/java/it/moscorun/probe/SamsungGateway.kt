package it.moscorun.probe

import android.app.Activity
import java.time.Instant

interface SamsungGateway {
    val available: Boolean
    val setupMessage: String
    suspend fun requestPermissions(activity: Activity): String
    suspend fun scan(start: Instant, end: Instant): ProbeReport
}
