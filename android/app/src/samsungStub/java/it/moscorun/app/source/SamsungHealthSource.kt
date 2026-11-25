package it.moscorun.app.source

import android.app.Activity
import android.content.Context
import it.moscorun.app.model.Provider
import java.time.Instant

/**
 * Stand-in used only to compile, test and lint the public sources without the proprietary Samsung Health Data
 * SDK (`-Pmoscorun.withoutSamsungSdk=true`). The build refuses to package an APK in this configuration, so this
 * class never reaches a phone: the official app always contains the real source (src/samsung).
 *
 * It has the same public surface as the real class and never pretends to read: every call reports that the
 * source is not available, and reading throws [SourceBlockedException] so the synchronisation waits.
 */
@Suppress("UNUSED_PARAMETER")
class SamsungHealthSource(context: Context) : WorkoutSource {
    override val provider = Provider.SAMSUNG_HEALTH

    override suspend fun status(): SourceStatus = SourceStatus.Unavailable(MESSAGE)

    suspend fun requestPermissions(activity: Activity): SourceStatus = status()

    override suspend fun read(start: Instant, end: Instant, pageToken: String?, pageSize: Int): SourcePage =
        throw SourceBlockedException(MESSAGE)

    internal companion object {
        const val MESSAGE = "Questa compilazione non include il Samsung Health Data SDK: usa Health Connect " +
            "oppure installa l'app ufficiale MoscoRun."
    }
}
