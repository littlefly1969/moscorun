package it.moscorun.app.server

import org.junit.After
import org.junit.Assert.assertArrayEquals
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertThrows
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test
import java.time.Instant
import java.time.ZoneId

private const val ID = "0193a7b2-1c2d-7e8f-9a0b-1c2d3e4f5a6b"

class ServerReadParserTest {
    @Test fun `page keeps the server order, skips malformed entries and reads the cursor`() {
        val json = """{"items":[
            {"id":"$ID","source":"samsung-health","sourceId":"uid:0","activityType":"RUNNING","startedAt":"2026-11-13T07:00:00+01:00",
             "endedAt":"2026-11-13T07:30:00+01:00","activeDurationSeconds":1700.5,"distanceM":5200,"energyKcal":410.2,
             "averageHeartRate":150,"maxHeartRate":null,"trackPointCount":1800},
            {"id":"non-un-uuid","activityType":"RUNNING","startedAt":"2026-11-14T07:00:00Z","endedAt":"2026-11-14T07:30:00Z"},
            {"id":"${ID.replace('a', 'b')}","activityType":"WALKING","startedAt":"2026-11-12T06:00:00Z","endedAt":"2026-11-12T06:10:00Z",
             "trackPointCount":0}
        ],"from":null,"to":"2026-11-16T08:00:00Z","nextCursor":"AbC-_123"}"""

        val page = ServerReadParser.page(json)

        assertEquals(listOf("RUNNING", "WALKING"), page.items.map { it.activityType })
        assertEquals("AbC-_123", page.nextCursor)
        val run = page.items[0]
        assertEquals("2026-11-13T06:00:00Z", run.startedAt)
        assertEquals("samsung-health", run.source)
        assertEquals("uid:0", run.sourceId)
        assertEquals(1700.5, run.seconds, 0.0)
        assertEquals(410.2, run.energyKcal!!, 0.0)
        assertNull(run.maxHeartRate)
        assertEquals(600.0, page.items[1].seconds, 0.0)
        assertNull(page.items[1].distanceM)
    }

    @Test fun `answer of a server without pagination is a single last page`() {
        val page = ServerReadParser.page("""{"items":[],"from":"x","to":"y"}""")

        assertTrue(page.items.isEmpty())
        assertNull(page.nextCursor)
    }

    @Test fun `detail lists every series the server holds, known or not`() {
        val json = """{"summary":{"id":"$ID","source":"samsung-health","sourceId":"u:0","activityType":"RUNNING",
            "startedAt":"2026-11-13T06:00:00Z","endedAt":"2026-11-13T06:30:00Z","trackPointCount":12},
            "series":[{"metric":"HeartRate","unit":"bpm","sampleCount":1800},{"metric":"Power","unit":"W","sampleCount":900},
                      {"metric":"GroundContact","unit":"ms","sampleCount":10},{"metric":"x y","unit":"?","sampleCount":1}]}"""

        val detail = ServerReadParser.detail(json)

        assertEquals(ID, detail.workout.id)
        assertEquals(listOf("HeartRate", "Power", "GroundContact"), detail.series.map { it.metric })
        assertEquals(RemoteSeriesInfo("GroundContact", "ms", 10), detail.series[2])
    }

    @Test fun `series are aligned arrays and can be filtered by metric`() {
        val json = """{"items":[
            {"metric":"HeartRate","unit":"bpm","times":[1000,2000,3000],"values":[140,150.5,160],"totalSamples":1800},
            {"metric":"Speed","unit":"m/s","times":[1000],"values":[2.9]}]}"""

        val hr = ServerReadParser.series(json, "HeartRate").single()
        val all = ServerReadParser.series(json)

        assertArrayEquals(longArrayOf(1000, 2000, 3000), hr.times)
        assertArrayEquals(doubleArrayOf(140.0, 150.5, 160.0), hr.values, 0.0)
        assertEquals(1800, hr.totalSamples)
        assertEquals("older servers do not report the total", 1, all[1].totalSamples)
        assertTrue(ServerReadParser.series(json, "Power").isEmpty())
        assertThrows(IllegalArgumentException::class.java) {
            ServerReadParser.series("""{"items":[{"metric":"HeartRate","unit":"bpm","times":[1],"values":[]}]}""")
        }
    }

    @Test fun `geojson route becomes track points with privacy counts`() {
        val json = """{"type":"Feature","bbox":[12.49,41.89,12.5,41.9],
            "geometry":{"type":"LineString","coordinates":[[12.49,41.89,20.5],[12.5,41.9]]},
            "properties":{"workoutId":"$ID","totalPoints":10,"returnedPoints":2,"hiddenStartPoints":3,"hiddenEndPoints":1,
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

    @Test fun `identity carries the account`() {
        val identity = ServerReadParser.identity("""{"deviceId":"$ID","name":"Telefono","kind":"PhoneBridge","accountId":"${ID.uppercase()}"}""")

        assertEquals(DeviceIdentity(ID, "Telefono", "PhoneBridge", ID), identity)
        assertThrows(RuntimeException::class.java) { ServerReadParser.identity("""{"deviceId":"$ID","accountId":"../x"}""") }
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
        assertEquals("410 kcal", WorkoutFormat.energy(410.2))
        assertEquals("ven 14 nov 2025, 08:00", WorkoutFormat.dateTime("2025-11-14T07:00:00Z", ZoneId.of("Europe/Rome")))
        assertEquals("Samsung Health", WorkoutFormat.source("samsung-health"))
        assertEquals("altra-fonte", WorkoutFormat.source("altra-fonte"))
    }

    @Test fun `known metrics get readable units and unknown ones keep the server's`() {
        assertEquals("Velocità", MetricDisplay.of("Speed", "m/s").label)
        assertEquals("10,8 km/h", MetricDisplay.of("Speed", "m/s").format(3.0))
        assertEquals("151 bpm", MetricDisplay.of("HeartRate", "bpm").format(150.6))
        assertEquals("GroundContact", MetricDisplay.of("GroundContact", "ms").label)
        assertEquals("240,0 ms", MetricDisplay.of("GroundContact", "ms").format(240.0))
    }
}

class MoscoRunServerReadTest {
    private var respond: (String) -> Pair<Int, String> = { 200 to """{"items":[]}""" }
    private val requests = mutableListOf<String>()
    private var headers: Map<String, String> = emptyMap()
    private lateinit var http: LoopbackServer

    @Before fun start() {
        http = LoopbackServer { line, h, _ ->
            requests += line
            headers = h
            respond(line)
        }
    }

    @After fun stop() = http.close()

    private fun server() = MoscoRunServer("http://127.0.0.1:${http.port}", "mr1_0123456789abcdef_secretsecret")

    private fun item(n: Int) = """{"id":"0193a7b2-1c2d-7e8f-9a0b-${"%012d".format(n)}","source":"samsung-health","sourceId":"s$n",
        "activityType":"RUNNING","startedAt":"2026-11-13T06:00:00Z","endedAt":"2026-11-13T06:30:00Z","trackPointCount":0}"""

    @Test fun `asks for one page with the device token`() {
        val result = server().workouts(pageSize = 25)

        assertEquals(ReadResult.Ok(WorkoutPage(emptyList(), null)), result)
        assertEquals("GET /api/v1/workouts?pageSize=25 HTTP/1.1", requests.single())
        assertEquals("Bearer mr1_0123456789abcdef_secretsecret", headers["authorization"])
    }

    @Test fun `cursor and interval are passed url-encoded`() {
        server().workouts(50, "AbC-_1", Instant.parse("2026-01-01T00:00:00Z"), Instant.parse("2026-02-01T00:00:00Z"))

        assertEquals("GET /api/v1/workouts?pageSize=50&cursor=AbC-_1&from=2026-01-01T00%3A00%3A00Z&to=2026-02-01T00%3A00%3A00Z HTTP/1.1",
            requests.single())
    }

    @Test fun `interval lookup follows the cursor to the last page`() {
        respond = { line ->
            200 to when {
                "cursor=c2" in line -> """{"items":[${item(3)}]}"""
                "cursor=c1" in line -> """{"items":[${item(2)}],"nextCursor":"c2"}"""
                else -> """{"items":[${item(1)}],"nextCursor":"c1"}"""
            }
        }

        val result = server().workoutsBetween(Instant.parse("2026-11-01T00:00:00Z"), Instant.parse("2026-11-30T00:00:00Z"))

        assertEquals(listOf("s1", "s2", "s3"), (result as ReadResult.Ok).value.map { it.sourceId })
        assertEquals(3, requests.size)
        assertTrue(requests.all { "from=2026-11-01" in it && "pageSize=200" in it })
    }

    @Test fun `interval lookup reports the failure of any page`() {
        respond = { line -> if ("cursor=" in line) 503 to "{}" else 200 to """{"items":[${item(1)}],"nextCursor":"c1"}""" }

        val result = server().workoutsBetween(Instant.parse("2026-11-01T00:00:00Z"), Instant.parse("2026-11-30T00:00:00Z"))

        assertTrue((result as ReadResult.Failed).transient)
    }

    @Test fun `series asks only for the chosen metric, reduced for the chart`() {
        respond = { 200 to """{"items":[{"metric":"Speed","unit":"m/s","times":[1],"values":[2.5]},
            {"metric":"HeartRate","unit":"bpm","times":[1,2],"values":[140,141],"totalSamples":900}]}""" }

        val result = server().series(ID, "HeartRate", maxPoints = 300)

        assertEquals("GET /api/v1/workouts/$ID/series?metric=HeartRate&maxPoints=300 HTTP/1.1", requests.single())
        assertEquals("an older server returns every series: only the requested one is used", 900, (result as ReadResult.Ok).value!!.totalSamples)
    }

    @Test fun `route requests the bounded number of points`() {
        respond = { 200 to """{"geometry":{"coordinates":[[1.0,2.0],[1.1,2.1]]},"properties":{"totalPoints":2,"times":[0,1000]}}""" }

        val result = server().route(ID, maxPoints = 300)

        assertEquals(2, (result as ReadResult.Ok).value.points.size)
        assertEquals("GET /api/v1/workouts/$ID/route?maxPoints=300 HTTP/1.1", requests.single())
    }

    @Test fun `identity checks the token without touching data`() {
        respond = { 200 to """{"deviceId":"$ID","name":"Telefono","kind":"PhoneBridge","accountId":"$ID"}""" }

        val result = server().identity()

        assertEquals("GET /api/v1/device HTTP/1.1", requests.single())
        assertEquals(ID, (result as ReadResult.Ok).value.accountId)
    }

    @Test fun `values that are not plain identifiers never reach the server`() {
        assertThrows(IllegalArgumentException::class.java) { server().route("../devices") }
        assertThrows(IllegalArgumentException::class.java) { server().detail("1 OR 1=1") }
        assertThrows(IllegalArgumentException::class.java) { server().series(ID, "HeartRate&maxPoints=100000") }
        assertThrows(IllegalArgumentException::class.java) { server().workouts(pageSize = 0) }
        assertTrue(requests.isEmpty())
    }

    @Test fun `errors are classified so the app knows what to tell the user`() {
        fun kind(code: Int, body: String = "{}"): ReadResult.Failed { respond = { code to body }; return server().workouts() as ReadResult.Failed }

        assertEquals(Failure.UNAUTHORIZED, kind(401).kind)
        assertEquals(Failure.FORBIDDEN, kind(403).kind)
        assertTrue(kind(403).unauthorized)
        assertEquals(Failure.NOT_FOUND, kind(404).kind)
        assertFalse(kind(404).unauthorized)
        assertEquals(Failure.UNAVAILABLE, kind(429).kind)
        assertTrue(kind(503).transient)
        assertEquals("Parametro non valido", kind(400, """{"title":"Parametro non valido"}""").message)
        assertTrue(kind(200, "<html>").message.startsWith("Risposta del server non riconosciuta"))
    }

    @Test fun `unreachable server is an offline failure, not an exception`() {
        val port = http.port
        http.close()

        val result = MoscoRunServer("http://127.0.0.1:$port", "mr1_0123456789abcdef_secretsecret", connectTimeoutMs = 2000).workouts()

        assertEquals(Failure.OFFLINE, (result as ReadResult.Failed).kind)
        assertTrue(result.transient)
    }
}
