package it.moscorun.probe

import org.junit.Assert.*
import org.junit.Test
import java.time.Instant
import java.time.Duration

class JsonTest {
    @Test fun `preserves precision nulls and numeric samples`() {
        val result=Json.tree(mapOf("time" to Instant.parse("2026-01-01T00:00:00.123456Z"),"missing" to null,
            "duration" to Duration.ofMillis(1234),"value" to 137.5)).asJsonObject
        assertEquals("2026-01-01T00:00:00.123456Z",result["time"].asString)
        assertTrue(result["missing"].isJsonNull)
        assertEquals(137.5,result["value"].asDouble,0.0)
        assertEquals("PT1.234S",result["duration"].asString)
    }
    @Test fun `denied permission remains distinct from an empty successful read`() {
        val denied=Json.tree(CategoryResult("Peso","permesso_negato")).asJsonObject
        val empty=Json.tree(CategoryResult("Peso","letto")).asJsonObject
        assertNotEquals(denied["status"],empty["status"])
        assertEquals(0,empty["records"].asInt)
    }
}
