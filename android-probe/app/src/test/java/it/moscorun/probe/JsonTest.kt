package it.moscorun.probe

import org.junit.Assert.*
import org.junit.Test
import java.time.Instant
import java.time.Duration
import androidx.health.connect.client.records.HeartRateRecord
import androidx.health.connect.client.records.ExerciseRoute
import androidx.health.connect.client.units.Length
import androidx.health.connect.client.records.metadata.Metadata

class JsonTest {
    @Test fun `long arrays in raw records are replaced by their size`() {
        val raw=Json.tree(mapOf("route" to List(500) { mapOf("lat" to 41.0) },"log" to listOf(1,2),
            "nested" to mapOf("samples" to List(21) { it }),"name" to "corsa"))

        val compact=Json.withoutLongArrays(raw).asJsonObject

        assertEquals("500 elementi omessi",compact["route"].asString)
        assertEquals(2,compact["log"].asJsonArray.size())
        assertEquals("21 elementi omessi",compact["nested"].asJsonObject["samples"].asString)
        assertEquals("corsa",compact["name"].asString)
    }

    @Test fun `preserves precision nulls and numeric samples`() {
        val result=Json.tree(mapOf("time" to Instant.parse("2026-01-01T00:00:00.123456Z"),"missing" to null,
            "duration" to Duration.ofMillis(1234),"value" to 137.5)).asJsonObject
        assertEquals("2026-01-01T00:00:00.123456Z",result["time"].asString)
        assertTrue(result["missing"].isJsonNull)
        assertEquals(137.5,result["value"].asDouble,0.0)
        assertEquals("PT1.234S",result["duration"].asString)
    }
    @Test fun `serializes a real Health Connect record with its native samples`() {
        val start=Instant.parse("2026-01-01T12:00:00Z")
        val record=HeartRateRecord(startTime=start,startZoneOffset=null,endTime=start.plusSeconds(2),endZoneOffset=null,
            samples=listOf(HeartRateRecord.Sample(start.plusMillis(250),137)),metadata=Metadata.manualEntry())
        val raw=Json.tree(record).asJsonObject
        assertEquals("2026-01-01T12:00:00Z",raw["startTime"].asString)
        assertEquals("2026-01-01T12:00:00.250Z",raw["samples"].asJsonArray[0].asJsonObject["time"].asString)
        assertEquals(137,raw["samples"].asJsonArray[0].asJsonObject["beatsPerMinute"].asInt)
        assertTrue(raw.has("metadata"))
    }
    @Test fun `native route keeps altitude and both accuracy fields`() {
        val route=ExerciseRoute(listOf(ExerciseRoute.Location(time=Instant.parse("2026-01-01T12:00:00.125Z"),
            latitude=41.9,longitude=12.5,altitude=Length.meters(30.0),horizontalAccuracy=Length.meters(2.0),verticalAccuracy=Length.meters(4.0))))
        val raw=Json.tree(route).asJsonObject["route"].asJsonArray[0].asJsonObject
        assertEquals("2026-01-01T12:00:00.125Z",raw["time"].asString)
        assertFalse(raw["verticalAccuracy"].isJsonNull)
        assertFalse(raw["horizontalAccuracy"].isJsonNull)
        assertFalse(raw["altitude"].isJsonNull)
    }
    @Test fun `denied permission remains distinct from an empty successful read`() {
        val denied=Json.tree(CategoryResult("Peso","permesso_negato")).asJsonObject
        val empty=Json.tree(CategoryResult("Peso","letto")).asJsonObject
        assertNotEquals(denied["status"],empty["status"])
        assertEquals(0,empty["records"].asInt)
    }
}
