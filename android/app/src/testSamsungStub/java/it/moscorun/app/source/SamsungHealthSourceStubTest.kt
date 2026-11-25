package it.moscorun.app.source

import android.app.Activity
import android.content.Context
import androidx.test.core.app.ApplicationProvider
import it.moscorun.app.model.Provider
import kotlinx.coroutines.runBlocking
import org.junit.Assert.assertEquals
import org.junit.Assert.assertThrows
import org.junit.Assert.assertTrue
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.Robolectric
import org.robolectric.RobolectricTestRunner
import java.time.Instant

/** Without the SDK the Samsung source must say so and never look ready or return data. */
@RunWith(RobolectricTestRunner::class)
class SamsungHealthSourceStubTest {
    private val context: Context = ApplicationProvider.getApplicationContext()
    private val source = SamsungHealthSource(context)

    @Test fun keeps_the_samsung_provider_identity() {
        assertEquals(Provider.SAMSUNG_HEALTH, source.provider)
    }

    @Test fun status_is_unavailable_with_a_reason() = runBlocking {
        val status = source.status()
        assertTrue(status is SourceStatus.Unavailable)
        assertEquals(SamsungHealthSource.MESSAGE, status.message)
    }

    @Test fun permission_request_does_not_grant_anything() = runBlocking {
        val activity = Robolectric.buildActivity(Activity::class.java).setup().get()
        assertTrue(source.requestPermissions(activity) is SourceStatus.Unavailable)
    }

    @Test fun reading_blocks_the_synchronisation_instead_of_returning_an_empty_page() {
        val error = assertThrows(SourceBlockedException::class.java) {
            runBlocking { source.read(Instant.EPOCH, Instant.EPOCH.plusSeconds(60), null, 10) }
        }
        assertEquals(SamsungHealthSource.MESSAGE, error.message)
    }
}
