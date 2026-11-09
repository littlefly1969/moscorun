package it.moscorun.probe

import com.samsung.android.sdk.health.data.data.HealthDataPoint
import com.samsung.android.sdk.health.data.data.entries.ExerciseLocation
import com.samsung.android.sdk.health.data.request.DataType
import com.samsung.android.sdk.health.data.request.DataTypes
import java.time.Instant
import java.time.ZoneOffset
import org.junit.Assert.*
import org.junit.Test

/** Uses real SDK DTOs only; no connection to Samsung Health or fabricated SDK classes. */
class SamsungSdkMappingTest {
    @Test fun `body measurement retains named SDK fields and client metadata`() {
        val time=Instant.parse("2026-11-09T08:00:00.125Z")
        val point=HealthDataPoint.builder().setStartTime(time,ZoneOffset.UTC)
            .setClientDataId("probe-fixture").setClientVersion(3)
            .addFieldData(DataType.BodyCompositionType.WEIGHT,88.25f).build()
        val raw=samsungSnapshot(point,DataTypes.BODY_COMPOSITION).asJsonObject
        assertEquals(88.25,raw["fields"].asJsonObject["WEIGHT"].asDouble,0.0)
        assertEquals("2026-11-09T08:00:00.125Z",raw["start"].asString)
        assertEquals("probe-fixture",raw["clientDataId"].asString)
        assertEquals(3,raw["clientVersion"].asInt)
        assertTrue(raw["fields"].asJsonObject["BODY_FAT"].isJsonNull)
    }
    @Test fun `GPS mapping preserves coordinate order time altitude and accuracy`() {
        val time=Instant.parse("2026-11-09T08:00:00.875Z")
        val native=ExerciseLocation.of(timestamp=time,longitude=12.5f,latitude=41.9f,altitude=35.5f,accuracy=4.25f)
        val point=samsungRoutePoints(listOf(native)).single()
        assertEquals(time.toString(),point.time)
        assertEquals(native.latitude.toDouble(),point.latitude,0.0)
        assertEquals(native.longitude.toDouble(),point.longitude,0.0)
        assertEquals(35.5,point.altitudeMeters!!,0.0)
        assertEquals(4.25,point.accuracyMeters!!,0.0)
        assertNull(point.verticalAccuracyMeters)
    }
}
