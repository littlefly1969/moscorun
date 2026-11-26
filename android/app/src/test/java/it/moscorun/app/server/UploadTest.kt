package it.moscorun.app.server

import it.moscorun.app.model.MetricSample
import it.moscorun.app.model.MetricSeries
import it.moscorun.app.model.Provider
import it.moscorun.app.model.SourceWorkout
import it.moscorun.app.model.TrackPoint
import it.moscorun.app.science.ScienceOutcome
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertThrows
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test
import java.time.Instant
import java.util.zip.GZIPInputStream

class UploadMapperTest {
    private val start = Instant.parse("2026-11-14T07:00:00Z")

    private fun sample(second: Int, value: Double) = MetricSample("2026-11-14T07:00:0${second}Z", value)

    private fun workout(id: String = "uid-1:0", revision: Long = 1700000000000, offset: Int? = 60, active: Double? = 3500.0,
        distance: Double? = 10_000.0, energy: Double? = 700.0, average: Double? = 0.0, max: Double? = 181.0,
        end: Instant = start.plusSeconds(3600), provider: Provider = Provider.SAMSUNG_HEALTH) =
        SourceWorkout(provider, id, revision, "RUNNING", start, end, offset, active, distance, energy, average, max,
            route = listOf(TrackPoint("2026-11-14T07:00:00Z", 41.89, 12.49, 21.0, 4.0), TrackPoint("2026-11-14T07:00:01Z", 41.8901, 12.4901)),
            series = listOf(
                MetricSeries("HeartRate", listOf(sample(0, 0.0), sample(1, 140.0), sample(2, Double.NaN))),
                MetricSeries("Speed", listOf(sample(0, 2.9))),
                MetricSeries("Power", emptyList()),
                MetricSeries("Altro", listOf(sample(0, 1.0)))))

    @Test fun `payload keeps source data and drops gaps and empty series`() {
        val p = UploadMapper.toPayload(workout())!!

        assertEquals("samsung-health", p.source)
        assertEquals("uid-1:0", p.sourceId)
        assertEquals(1700000000000, p.revision)
        assertEquals("2026-11-14T07:00:00Z", p.startedAt)
        assertEquals("2026-11-14T08:00:00Z", p.endedAt)
        assertEquals(2, p.track.size)
        assertEquals(21.0, p.track[0].altitudeM!!, 0.0)
        assertNull(p.track[1].altitudeM)
        assertEquals(listOf("HeartRate", "Speed"), p.series.map { it.metric })
        assertEquals(listOf(140.0), p.series[0].samples.map { it.value })
        assertNull("0 bpm significa misura assente", p.averageHeartRate)
        assertEquals(181.0, p.maxHeartRate!!, 0.0)
    }

    @Test fun `source code follows the provider`() {
        assertEquals("health-connect", UploadMapper.toPayload(workout(provider = Provider.HEALTH_CONNECT))!!.source)
    }

    @Test fun `summary values outside server ranges become missing`() {
        val p = UploadMapper.toPayload(workout(revision = -5, offset = 2000, active = 99_999.0, distance = -1.0,
            energy = Double.NaN, average = 300.0, max = 10.0))!!

        assertEquals(0, p.revision)
        assertNull(p.utcOffsetMinutes)
        assertNull(p.activeDurationSeconds)
        assertNull(p.distanceM)
        assertNull(p.energyKcal)
        assertNull(p.averageHeartRate)
        assertNull(p.maxHeartRate)
    }

    @Test fun `workouts with an identity or duration the server cannot store are not uploadable`() {
        assertNull(UploadMapper.toPayload(workout(id = "id con spazi")))
        assertNull(UploadMapper.toPayload(workout(id = "x".repeat(129))))
        assertNull(UploadMapper.toPayload(workout(end = start)))
    }

    @Test fun `activity codes are sanitized for the server alphabet`() {
        assertEquals("CORSA_SU_STRADA", UploadMapper.activityCode("CORSA SU STRADA"))
        assertEquals("UNKNOWN", UploadMapper.activityCode(""))
        assertEquals(64, UploadMapper.activityCode("x".repeat(100)).length)
    }

    @Test fun `json and fingerprint are deterministic`() {
        val a = UploadMapper.json(UploadMapper.toPayload(workout())!!)
        val b = UploadMapper.json(UploadMapper.toPayload(workout())!!)
        assertEquals(a, b)
        assertEquals(UploadMapper.fingerprint(a), UploadMapper.fingerprint(b))
        assertTrue(a.contains("\"lat\":41.89"))
        assertTrue("null espliciti per i valori mancanti non servono", !a.contains("\"altitudeM\":null"))
    }

    /**
     * The fingerprint of an unchanged workout must not change between app versions: the ledger of the previous
     * version is imported, and a different fingerprint would send the whole history again.
     */
    @Test fun `payload format is the one fingerprinted by the previous app versions`() {
        val json = UploadMapper.json(UploadMapper.toPayload(workout())!!)

        assertEquals("""{"source":"samsung-health","sourceId":"uid-1:0","revision":1700000000000,"activityType":"RUNNING",""" +
            """"startedAt":"2026-11-14T07:00:00Z","endedAt":"2026-11-14T08:00:00Z","utcOffsetMinutes":60,"activeDurationSeconds":3500.0,""" +
            """"distanceM":10000.0,"energyKcal":700.0,"maxHeartRate":181.0,""" +
            """"track":[{"time":"2026-11-14T07:00:00Z","lat":41.89,"lon":12.49,"altitudeM":21.0,"accuracyM":4.0},""" +
            """{"time":"2026-11-14T07:00:01Z","lat":41.8901,"lon":12.4901}],""" +
            """"series":[{"metric":"HeartRate","samples":[{"time":"2026-11-14T07:00:01Z","value":140.0}]},""" +
            """{"metric":"Speed","samples":[{"time":"2026-11-14T07:00:00Z","value":2.9}]}]}""", json)
        assertEquals(64, UploadMapper.fingerprint(json).length)
    }

    @Test fun `base url must be https without credentials`() {
        assertEquals("https://moscorun.example.it", MoscoRunServer.normalizeBaseUrl(" https://moscorun.example.it/ "))
        assertEquals("https://host:8443/sub", MoscoRunServer.normalizeBaseUrl("https://host:8443/sub/"))
        assertEquals("http://127.0.0.1:5080", MoscoRunServer.normalizeBaseUrl("http://127.0.0.1:5080"))
        listOf("http://moscorun.example.it", "https://u:p@host", "https://host?x=1", "ftp://host", "nonsense", "https://ho st").forEach {
            assertThrows(it, IllegalArgumentException::class.java) { MoscoRunServer.normalizeBaseUrl(it) }
        }
    }

    @Test fun `token format is checked`() {
        assertTrue(MoscoRunServer.validToken("mr1_0123456789abcdef_ABCdef-_0123456789"))
        assertFalse(MoscoRunServer.validToken("Bearer mr1_abc"))
        assertFalse(MoscoRunServer.validToken("mr1_short"))
    }

    @Test fun `problem details are summarized`() {
        assertEquals("track: Punto 3 non valido", MoscoRunServer.problemDetail("""{"errors":{"track":["Punto 3 non valido"]}}"""))
        assertEquals("Conflitto", MoscoRunServer.problemDetail("""{"title":"Conflitto"}"""))
        assertNull(MoscoRunServer.problemDetail("<html>"))
        assertNull(MoscoRunServer.problemDetail(null))
    }
}

class MoscoRunServerUploadTest {
    private var status = 201
    private var responseBody = "{}"
    private var received: String? = null
    private var headers: Map<String, String> = emptyMap()
    private lateinit var http: LoopbackServer

    @Before fun start() {
        http = LoopbackServer { _, h, body ->
            headers = h
            received = GZIPInputStream(body.inputStream()).readBytes().decodeToString()
            status to responseBody
        }
    }

    @After fun stop() = http.close()

    private fun server() = MoscoRunServer("http://127.0.0.1:${http.port}", "mr1_0123456789abcdef_secretsecret")

    @Test fun `sends gzip json with bearer token and returns the server id`() {
        responseBody = """{"id":"0193a7b2-1c2d-7e8f-9a0b-1c2d3e4f5a6b","source":"samsung-health"}"""

        val result = server().upload("""{"a":1}""")

        assertEquals(UploadOutcome.CREATED, result.outcome)
        assertEquals("0193a7b2-1c2d-7e8f-9a0b-1c2d3e4f5a6b", result.serverId)
        assertEquals("""{"a":1}""", received)
        assertEquals("Bearer mr1_0123456789abcdef_secretsecret", headers["authorization"])
        assertEquals("gzip", headers["content-encoding"])
    }

    @Test fun `maps server answers to outcomes`() {
        mapOf(200 to UploadOutcome.STORED, 409 to UploadOutcome.CONFLICT, 400 to UploadOutcome.INVALID,
            401 to UploadOutcome.UNAUTHORIZED, 403 to UploadOutcome.UNAUTHORIZED, 413 to UploadOutcome.TOO_LARGE,
            429 to UploadOutcome.RETRY, 500 to UploadOutcome.RETRY, 503 to UploadOutcome.RETRY).forEach { (code, outcome) ->
            status = code
            responseBody = """{"title":"t$code"}"""
            assertEquals("HTTP $code", outcome, server().upload("{}").outcome)
        }
    }

    @Test fun `conflict and validation answers carry the server explanation`() {
        status = 409
        responseBody = """{"title":"Conflitto di revisione","detail":"Lo stesso allenamento è già registrato con contenuto diverso."}"""
        assertEquals("Lo stesso allenamento è già registrato con contenuto diverso.", server().upload("{}").detail)
    }

    @Test fun `unreachable server asks for a retry instead of throwing`() {
        val port = http.port
        http.close()
        val result = MoscoRunServer("http://127.0.0.1:$port", "mr1_0123456789abcdef_secretsecret", connectTimeoutMs = 2000).upload("{}")
        assertEquals(UploadOutcome.RETRY, result.outcome)
    }
}

class MoscoRunServerObservationsTest {
    private var status = 200
    private var responseBody = """{"received":2,"imported":1,"cursor":null}"""
    private var requestLine: String? = null
    private var received: String? = null
    private lateinit var http: LoopbackServer

    @Before fun start() {
        http = LoopbackServer { line, _, body ->
            requestLine = line
            received = GZIPInputStream(body.inputStream()).readBytes().decodeToString()
            status to responseBody
        }
    }

    @After fun stop() = http.close()

    private fun server() = MoscoRunServer("http://127.0.0.1:${http.port}", "mr1_0123456789abcdef_secretsecret")

    @Test fun `observations are posted compressed to the scientific endpoint`() {
        val result = server().postObservations("""{"source":"health-connect"}""")

        assertEquals(ScienceOutcome.STORED, result.outcome)
        assertEquals(1, result.imported)
        assertEquals("POST /api/v1/ingest/observations HTTP/1.1", requestLine)
        assertEquals("""{"source":"health-connect"}""", received)
    }

    @Test fun `maps server answers to scientific outcomes`() {
        mapOf(400 to ScienceOutcome.INVALID, 413 to ScienceOutcome.INVALID, 409 to ScienceOutcome.CONFLICT,
            401 to ScienceOutcome.UNAUTHORIZED, 403 to ScienceOutcome.UNAUTHORIZED, 404 to ScienceOutcome.UNSUPPORTED,
            429 to ScienceOutcome.RETRY, 503 to ScienceOutcome.RETRY).forEach { (code, outcome) ->
            status = code
            responseBody = """{"title":"t$code","errors":{"samples[0].metric":["non ammessa"]}}"""
            assertEquals("HTTP $code", outcome, server().postObservations("{}").outcome)
        }
        status = 400
        assertEquals("samples[0].metric: non ammessa", server().postObservations("{}").detail)
    }

    @Test fun `unreachable server asks for a retry instead of throwing`() {
        val port = http.port
        http.close()
        val result = MoscoRunServer("http://127.0.0.1:$port", "mr1_0123456789abcdef_secretsecret", connectTimeoutMs = 2000).postObservations("{}")
        assertEquals(ScienceOutcome.RETRY, result.outcome)
    }
}
