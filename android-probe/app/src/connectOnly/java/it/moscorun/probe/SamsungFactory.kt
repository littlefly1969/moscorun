package it.moscorun.probe

import android.app.Activity
import java.time.Instant

fun samsungGateway(activity: Activity): SamsungGateway = object : SamsungGateway {
    override val available = false
    override val setupMessage = "Questa APK contiene Health Connect. Per provare l'accesso diretto Samsung serve la variante withSamsung con la libreria ufficiale Samsung. Le istruzioni sono nel repository."
    override suspend fun requestPermissions(activity: Activity) = setupMessage
    override suspend fun scan(start: Instant, end: Instant): ProbeReport = error(setupMessage)
}
