package it.moscorun.probe

import org.junit.After
import org.junit.Assert.*
import org.junit.Before
import org.junit.Test
import java.time.ZoneId

class ServerReadParserTest {
    private val id = "0193a7b2-1c2d-7e8f-9a0b-1c2d3e4f5a6b"

    @Test fun `workouts are parsed newest first and malformed entries are skipped`() {
        val json = """{"items":[
            {"id":"$id","activityType":"RUNNING","startedAt":"2026-11-13T07:00:00+01:00","endedAt":"2026-11-13T07:30:00+01:00",
             "activeDurationSeconds":1700.5,"distanceM":5200,"averageHeartRate":150,"maxHeartRate":null,"trackPointCount":1800},
            {"id":"non-un-uuid","activityType":"RUNNING","startedAt":"2026-11-14T07:00:00Z","endedAt":"2026-11-14T07:30:00Z"},
            {"id":"${id.replace('a', 'b')}","activityType":"WALKING","startedAt":"2026-11-14T06:00:00Z","endedAt":"2026-11-14T06:10:00Z",
             "trackPointCount":0}
        ],"from":"x","to":"y"}"""

        val list = ServerReadParser.workouts(json)

        assertEquals(listOf("WALKING", "RUNNING"), list.map { it.activityType })
        val run = list[1]
        assertEquals("2026-11-13T06:00:00Z", run.startedAt)
        assertEquals(1700.5, run.seconds, 0.0)
        assertNull(run.maxHeartRate)
        assertEquals(600.0, list[0].seconds, 0.0)
        assertNull(list[0].distanceM)
    }

    @Test fun `geojson route becomes track points with privacy counts`() {
        val json = """{"type":"Feature","bbox":[12.49,41.89,12.5,41.9],
            "geometry":{"type":"LineString","coordinates":[[12.49,41.89,20.5],[12.5,41.9]]},
            "properties":{"workoutId":"$id","totalPoints":10,"returnedPoints":2,"hiddenStartPoints":3,"hiddenEndPoints":1,
            "times":[1763103600000,1763103601000]}}"""

        val route = ServerReadParser.route(json)

        assertEquals(2, route.points.size)
        assertEquals(41.89, route.points[0].latitude, 0.0)
        assertEquals(12.49, route.points[0].longitude, 0.0)
        assertEquals(20.5, route.points[0].altitudeMeters!!, 0.0)
        assertNull(route.points[1].altitudeMeters)
        assertEquals("2025-11-14T07:00:01Z", route.points[1].time)
        assertEquals(10, route.totalPoints)
        assertEquals(3, route.hiddenStartPoints)
        assertEquals(1, route.hiddenEndPoints)
    }

    @Test fun `route with misaligned times is rejected`() {
        val json = """{"geometry":{"coordinates":[[12.49,41.89]]},"properties":{"totalPoints":1,"times":[]}}"""
        assertThrows(IllegalArgumentException::class.java) { ServerReadParser.route(json) }
    }
}

class WorkoutFormatTest {
    @Test fun `labels and units match the web dashboard`() {
        assertEquals("Corsa", WorkoutFormat.activity("running"))
        assertEquals("Nordic walking", WorkoutFormat.activity("NORDIC_WALKING"))
        assertEquals("850 m", WorkoutFormat.distance(850.4))
        assertEquals("5,2 km", WorkoutFormat.distance(5200.0))
        assertEquals("—", WorkoutFormat.distance(null))
        assertEquals("28:20", WorkoutFormat.duration(1700.4))
        assertEquals("1:02:05", WorkoutFormat.duration(3725.0))
        assertEquals("5:27 /km", WorkoutFormat.pace(1700.0, 5200.0))
        assertEquals("—", WorkoutFormat.pace(60.0, 50.0))
        assertEquals("151 bpm", WorkoutFormat.bpm(150.6))
        assertEquals("ven 14 nov 2025, 08:00", WorkoutFormat.dateTime("2025-11-14T07:00:00Z", ZoneId.of("Europe/Rome")))
    }
}

class MoscoRunServerReadTest {
    private var status = 200
    private var responseBody = """{"items":[]}"""
    private val requests = mutableListOf<String>()
    private var headers: Map<String, String> = emptyMap()
    private lateinit var http: LoopbackServer

    @Before fun start() {
        http = LoopbackServer { line, h, _ ->
            requests += line
            headers = h
            status to responseBody
        }
    }

    @After fun stop() = http.close()

    private fun server() = MoscoRunServer("http://127.0.0.1:${http.port}", "mr1_0123456789abcdef_secretsecret")

    @Test fun `lists workouts with the device token`() {
        val result = server().workouts()

        assertEquals(ReadResult.Ok(emptyList<RemoteWorkout>()), result)
        assertEquals("GET /api/v1/workouts HTTP/1.1", requests.single())
        assertEquals("Bearer mr1_0123456789abcdef_secretsecret", headers["authorization"])
    }

    @Test fun `route requests the bounded number of points`() {
        responseBody = """{"geometry":{"coordinates":[[1.0,2.0],[1.1,2.1]]},"properties":{"totalPoints":2,"times":[0,1000]}}"""
        val id = "0193a7b2-1c2d-7e8f-9a0b-1c2d3e4f5a6b"

        val result = server().route(id, maxPoints = 300)

        assertEquals(2, (result as ReadResult.Ok).value.points.size)
        assertEquals("GET /api/v1/workouts/$id/route?maxPoints=300 HTTP/1.1", requests.single())
    }

    @Test fun `ids that are not uuids never reach the server`() {
        assertThrows(IllegalArgumentException::class.java) { server().route("../devices") }
        assertTrue(requests.isEmpty())
    }

    @Test fun `errors become readable failures`() {
        status = 403
        assertTrue((server().workouts() as ReadResult.Failed).unauthorized)
        status = 401
        assertTrue((server().workouts() as ReadResult.Failed).unauthorized)
        status = 404
        assertFalse((server().workouts() as ReadResult.Failed).unauthorized)
        status = 200
        responseBody = "<html>"
        assertTrue((server().workouts() as ReadResult.Failed).message.startsWith("Risposta del server non riconosciuta"))
    }
}
