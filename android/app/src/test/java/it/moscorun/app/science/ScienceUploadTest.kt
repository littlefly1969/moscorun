package it.moscorun.app.science

import com.google.gson.JsonParser
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertThrows
import org.junit.Test
import java.time.Instant

class ScienceUploadTest {
    private fun observation(id: String = "hc:1") = ScienceObservation(id, 1795158000123, "hrvRmssd", "ms", 42.5,
        Instant.parse("2026-11-20T05:30:00Z"), ScienceMethod.PROVIDER_REPORTED, "com.sec.android.app.shealth",
        "HeartRateVariabilityRmssdRecord", "ms", 42.5)

    @Test fun `json follows the server contract`() {
        val body = JsonParser.parseString(ScienceUpload.json("health-connect", listOf(observation()))).asJsonObject
        val sample = body.getAsJsonArray("samples")[0].asJsonObject

        assertEquals("health-connect", body["source"].asString)
        assertFalse(body.has("page"))
        assertEquals("hc:1", sample["externalId"].asString)
        assertEquals("1795158000123", sample["revision"].asString)
        assertEquals(1795158000123, sample["revisionOrder"].asLong)
        assertEquals("2026-11-20T05:30:00Z", sample["time"].asString)
        assertEquals("ProviderReported", sample["method"].asString)
        assertEquals("hc-science-v1", sample.getAsJsonObject("native")["mappingVersion"].asString)
    }

    @Test fun `batches are bounded by the server limit`() {
        assertThrows(IllegalArgumentException::class.java) { ScienceUpload.json("health-connect", emptyList()) }
        assertThrows(IllegalArgumentException::class.java) {
            ScienceUpload.json("health-connect", (0..ScienceUpload.MAX_BATCH).map { observation("hc:$it") })
        }
    }
}
