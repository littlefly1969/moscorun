package it.moscorun.app.science

import androidx.health.connect.client.records.SleepSessionRecord
import androidx.health.connect.client.records.Vo2MaxRecord
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test
import java.time.Instant

class HealthConnectScienceMappingTest {
    private val origin = RecordOrigin("abc-123", Instant.parse("2026-11-20T07:00:00.123Z"), "com.sec.android.app.shealth")
    private val at = Instant.parse("2026-11-20T05:30:00Z")

    private fun one(record: ScienceRecord) = HealthConnectScienceMapping.observations(record).single()

    @Test fun `identity revision and provenance come from the record`() {
        val o = one(ScienceRecord.HrvRmssd(origin, at, 42.5))

        assertEquals("hc:abc-123", o.externalId)
        assertEquals(1795158000123, o.revision)
        assertEquals("hrvRmssd", o.metric)
        assertEquals("ms", o.unit)
        assertEquals(42.5, o.value!!, 0.0)
        assertEquals(at, o.time)
        assertEquals(ScienceMethod.PROVIDER_REPORTED, o.method)
        assertEquals("com.sec.android.app.shealth", o.providerAlgorithm)
        assertEquals("HeartRateVariabilityRmssdRecord", o.nativeMetric)
    }

    @Test fun `each record type maps to its catalog metric and canonical unit`() {
        val cases = mapOf(
            ScienceRecord.RestingHeartRate(origin, at, 48) to ("restingHeartRate" to "bpm"),
            ScienceRecord.OxygenSaturation(origin, at, 96.0) to ("spo2" to "%"),
            ScienceRecord.RespiratoryRate(origin, at, 14.5) to ("respirationRate" to "1/min"),
            ScienceRecord.Vo2Max(origin, at, 52.0, Vo2MaxRecord.MEASUREMENT_METHOD_HEART_RATE_RATIO) to ("vo2max" to "mL/kg/min"))
        cases.forEach { (record, expected) ->
            val o = one(record)
            assertEquals(expected, o.metric to o.unit)
        }
        assertEquals(ScienceMethod.MEASURED, one(ScienceRecord.OxygenSaturation(origin, at, 96.0)).method)
    }

    @Test fun `vo2max is an estimate with the method named, except a metabolic cart measurement`() {
        val estimate = one(ScienceRecord.Vo2Max(origin, at, 52.0, Vo2MaxRecord.MEASUREMENT_METHOD_HEART_RATE_RATIO))
        val other = one(ScienceRecord.Vo2Max(origin, at, 52.0, Vo2MaxRecord.MEASUREMENT_METHOD_OTHER))
        val lab = one(ScienceRecord.Vo2Max(origin, at, 52.0, Vo2MaxRecord.MEASUREMENT_METHOD_METABOLIC_CART))

        assertEquals(ScienceMethod.PROVIDER_ESTIMATED, estimate.method)
        assertEquals("com.sec.android.app.shealth:heart-rate-ratio", estimate.providerAlgorithm)
        assertEquals("com.sec.android.app.shealth:other", other.providerAlgorithm)
        assertEquals(ScienceMethod.MEASURED, lab.method)
    }

    @Test fun `values outside the physiological range are gaps, not data`() {
        listOf(
            ScienceRecord.RestingHeartRate(origin, at, 0),
            ScienceRecord.HrvRmssd(origin, at, Double.NaN),
            ScienceRecord.HrvRmssd(origin, at, 0.0),
            ScienceRecord.OxygenSaturation(origin, at, 0.0),
            ScienceRecord.RespiratoryRate(origin, at, 200.0),
            ScienceRecord.Vo2Max(origin, at, 5.0, Vo2MaxRecord.MEASUREMENT_METHOD_OTHER),
        ).forEach { assertTrue(it.toString(), HealthConnectScienceMapping.observations(it).isEmpty()) }
    }

    @Test fun `sleep reports time in bed and, only with stages, the asleep stages clipped to the session`() {
        val start = Instant.parse("2026-11-19T22:00:00Z")
        val end = Instant.parse("2026-11-20T06:00:00Z")
        val stages = listOf(
            SleepStage(start.minusSeconds(600), start.plusSeconds(3600), SleepSessionRecord.STAGE_TYPE_LIGHT),
            SleepStage(start.plusSeconds(3600), start.plusSeconds(7200), SleepSessionRecord.STAGE_TYPE_AWAKE),
            SleepStage(start.plusSeconds(7200), start.plusSeconds(14400), SleepSessionRecord.STAGE_TYPE_DEEP),
            SleepStage(start.plusSeconds(14400), end, SleepSessionRecord.STAGE_TYPE_REM))

        val withStages = HealthConnectScienceMapping.observations(ScienceRecord.Sleep(origin, start, end, stages)).associateBy { it.metric }
        val withoutStages = HealthConnectScienceMapping.observations(ScienceRecord.Sleep(origin, start, end, emptyList()))

        assertEquals(8 * 3600.0, withStages.getValue("sleepInBedDuration").value!!, 0.0)
        assertEquals(3600.0 + 7200.0 + 4 * 3600.0, withStages.getValue("sleepDuration").value!!, 0.0)
        assertEquals(end, withStages.getValue("sleepDuration").time)
        assertEquals(listOf("sleepInBedDuration"), withoutStages.map { it.metric })
    }

    @Test fun `sleep sessions that are empty or longer than a day are left out`() {
        assertTrue(HealthConnectScienceMapping.observations(ScienceRecord.Sleep(origin, at, at, emptyList())).isEmpty())
        assertTrue(HealthConnectScienceMapping.observations(ScienceRecord.Sleep(origin, at, at.plusSeconds(25 * 3600), emptyList())).isEmpty())
    }

    @Test fun `a record without identity is not sent and a blank origin leaves the provider unknown`() {
        assertTrue(HealthConnectScienceMapping.observations(ScienceRecord.HrvRmssd(origin.copy(id = ""), at, 40.0)).isEmpty())
        assertNull(one(ScienceRecord.HrvRmssd(origin.copy(app = ""), at, 40.0)).providerAlgorithm)
    }
}
