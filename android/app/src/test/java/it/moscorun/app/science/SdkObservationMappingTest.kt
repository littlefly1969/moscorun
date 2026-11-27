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
    @Test fun `daily aggregate preserves interval identity without inventing a source revision order`() {
        val end = at.plusSeconds(86400)
        val first = SdkObservationMapping.samsungDailyAggregate("steps", "1", 5000.0, at, end,
            "StepsType.TOTAL", ScienceMethod.PROVIDER_REPORTED)!!
        val corrected = SdkObservationMapping.samsungDailyAggregate("steps", "1", 5100.0, at, end,
            "StepsType.TOTAL", ScienceMethod.PROVIDER_REPORTED)!!
        assertEquals(first.externalId, corrected.externalId)
        assertNotEquals(first.externalRevision, corrected.externalRevision)
        assertNull(ScienceUpload.item(first).revisionOrder)
        assertEquals(end.minusMillis(1), first.time)
        assertNull(SdkObservationMapping.samsungDailyAggregate("steps", "1", null, at, end,
            "StepsType.TOTAL", ScienceMethod.PROVIDER_REPORTED))
    }
    @Test fun `monitor notifications are explicit provider results not diagnoses or inferred negatives`() {
        assertEquals(1.0, SdkObservationMapping.monitorStatus("DETECTED", false))
        assertNull(SdkObservationMapping.monitorStatus("NOT_DETECTED", false))
        assertEquals(0.0, SdkObservationMapping.monitorStatus("NOT_DETECTED", true))
        assertNull(SdkObservationMapping.monitorStatus("UNDEFINED", true))
        assertNull(SdkObservationMapping.monitorStatus("unknown", true))
    }
}
