package it.moscorun.app.science

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test
import java.time.Instant

class SdkObservationMappingTest {
    private val at = Instant.parse("2026-11-26T10:00:00Z")
    private val origin = RecordOrigin("record", at, "com.sec.android.app.shealth")

    @Test fun `native data and identity survive mapping and upload`() {
        val result = SdkObservationMapping.value(origin, "sh", "samsung-data-v1", "skeletalMuscleMass", "kg",
            31.2, at, "SKELETAL_MUSCLE_MASS", ScienceMethod.PROVIDER_ESTIMATED)!!
        assertEquals("sh:record", result.externalId)
        assertEquals("skeletalMuscleMass", result.metric)
        assertEquals(31.2, result.value!!, 0.0)
        assertEquals("samsung-data-v1", ScienceUpload.item(result).native.mappingVersion)
        assertEquals("SKELETAL_MUSCLE_MASS", ScienceUpload.item(result).native.metric)
        assertEquals("com.sec.android.app.shealth", result.providerAlgorithm)
    }

    @Test fun `no fabricated value for absent data or invalid numeric values`() {
        assertNull(SdkObservationMapping.value(origin, "sh", "v1", "fatMass", "kg", null, at, "BODY_FAT_MASS"))
        assertNull(SdkObservationMapping.value(origin, "sh", "v1", "fatMass", "kg", Double.NaN, at, "BODY_FAT_MASS"))
        assertEquals(0.0, SdkObservationMapping.value(origin, "hc", "v1", "steps", "1", 0.0, at, "count")!!.value!!, 0.0)
    }

    @Test fun `new consent cannot reuse an incompatible historical checkpoint`() {
        val old = ScienceAvailability(setOf(ScienceMetric.HRV_RMSSD))
        val expanded = old.copy(metrics = old.metrics + ScienceMetric.WEIGHT)
        assertNotEquals(ScienceCapabilities.checkpoint("health-connect", "v1", old),
            ScienceCapabilities.checkpoint("health-connect", "v1", expanded))
        assertTrue(ScienceCapabilities.fieldsFor(ScienceCapabilities.samsung).containsKey("skeletalMuscleMass"))
        assertTrue(ScienceCapabilities.fieldsFor(ScienceCapabilities.healthConnect).containsKey("weight"))
    }
}
