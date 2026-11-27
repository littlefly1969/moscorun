package it.moscorun.app.science

import com.google.gson.JsonParser
import it.moscorun.sensor.SensorChannel
import it.moscorun.sensor.SensorChunk
import it.moscorun.sensor.SensorSample
import org.junit.Assert.*
import org.junit.Test
import java.util.UUID

class SensorChunkTest {
    private val session = UUID.randomUUID()
    private val channel = SensorChannel("ecg", "mV", "EcgSet.ECG_MV")

    @Test fun `strict chunks preserve every original sample and timestamp including equal times`() {
        val points = listOf(SensorSample(1000, 0.1), SensorSample(1001, null),
            SensorSample(1001, 0.2), SensorSample(1003, Double.NaN), SensorSample(1004, -0.4))
        val payloads = SensorChunk.encode(session, channel, points, 2000, chunkSize = 2)
        assertEquals(listOf(2, 2, 1), payloads.map { it.samples })
        assertEquals(3, payloads.map { it.id }.distinct().size)
        val samples = payloads.flatMap { payload ->
            val obj = JsonParser.parseString(payload.json).asJsonObject
            assertEquals("samsung-sensor", obj["source"].asString)
            assertEquals("ecg", obj["metric"].asString)
            assertEquals("mV", obj["unit"].asString)
            assertEquals(payload.id, obj["externalId"].asString)
            obj.getAsJsonArray("samples").map { it.asJsonObject }
        }
        assertEquals(5, samples.size)
        assertEquals(samples[1]["time"], samples[2]["time"])
        assertTrue(samples[1]["value"].isJsonNull)
        assertTrue(samples[3]["value"].isJsonNull)
        assertEquals(-0.4, samples[4]["value"].asDouble, 0.0)
    }

    @Test fun `future time is not shifted or silently accepted`() {
        assertThrows(IllegalArgumentException::class.java) {
            SensorChunk.encode(session, channel, listOf(SensorSample(2001, 0.1)), 2000)
        }
    }

    @Test fun `accelerometer uses the vendor scale`() {
        assertEquals(0.0, SensorChunk.acceleration(0), 0.0)
        assertEquals(9.81, SensorChunk.acceleration(4096), 0.01)
    }
}
