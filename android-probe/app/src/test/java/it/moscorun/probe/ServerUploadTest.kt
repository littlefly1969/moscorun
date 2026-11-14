package it.moscorun.probe

import com.google.gson.JsonParser
import org.junit.After
import org.junit.Assert.*
import org.junit.Before
import org.junit.Test
import java.io.BufferedInputStream
import java.io.ByteArrayOutputStream
import java.io.IOException
import java.io.InputStream
import java.io.OutputStream
import java.net.InetAddress
import java.net.ServerSocket
import kotlin.concurrent.thread
import java.util.zip.GZIPInputStream

class ServerUploadTest {
    private fun workout(id: String = "uid-1:0", summary: WorkoutSummary? = WorkoutSummary("RUNNING", 1700000000000, 60, 3500.0,
        10_000.0, 700.0, 0.0, 181.0), series: List<Series> = listOf(
        Series("Frequenza cardiaca", "letto", listOf(sample(0, 0.0), sample(1, 140.0), sample(2, Double.NaN)), metric = "HeartRate"),
        Series("Velocità", "letto", listOf(sample(0, 2.9)), metric = "Speed"),
        Series("Potenza", "letto", emptyList(), metric = "Power"),
        Series("Altro", "letto", listOf(sample(0, 1.0))))) =
        Workout(id, "RUNNING", "2026-11-14T07:00:00Z", "2026-11-14T08:00:00Z", "app", JsonParser.parseString("{}"),
            route = listOf(TrackPoint("2026-11-14T07:00:00Z", 41.89, 12.49, 21.0, 4.0), TrackPoint("2026-11-14T07:00:01Z", 41.8901, 12.4901)),
            series = series, summary = summary, uploadSource = "samsung-health")

    private fun sample(second: Int, value: Double) = Sample("2026-11-14T07:00:0${second}Z", value, "u", "r", "s")

    @Test fun `payload keeps source data and drops gaps and empty series`() {
        val p = UploadMapper.toPayload(workout())!!

        assertEquals("samsung-health", p.source)
        assertEquals("uid-1:0", p.sourceId)
        assertEquals(1700000000000, p.revision)
        assertEquals(2, p.track.size)
        assertEquals(21.0, p.track[0].altitudeM!!, 0.0)
        assertNull(p.track[1].altitudeM)
        assertEquals(listOf("HeartRate", "Speed"), p.series.map { it.metric })
        assertEquals(listOf(140.0), p.series[0].samples.map { it.value })
        assertNull("0 bpm significa misura assente", p.averageHeartRate)
        assertEquals(181.0, p.maxHeartRate!!, 0.0)
    }

    @Test fun `summary values outside server ranges become missing`() {
        val p = UploadMapper.toPayload(workout(summary = WorkoutSummary("RUNNING", -5, 2000, 99_999.0, -1.0, Double.NaN, 300.0, 10.0)))!!

        assertEquals(0, p.revision)
        assertNull(p.utcOffsetMinutes)
        assertNull(p.activeDurationSeconds)
        assertNull(p.distanceM)
        assertNull(p.energyKcal)
        assertNull(p.averageHeartRate)
        assertNull(p.maxHeartRate)
    }

    @Test fun `workouts without upload source or with invalid id are not uploadable`() {
        assertNull(UploadMapper.toPayload(workout(summary = null)))
        assertNull(UploadMapper.toPayload(workout(id = "id con spazi")))
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

    @Test fun `base url must be https without credentials`() {
        assertEquals("https://moscorun.example.it", MoscoRunServer.normalizeBaseUrl(" https://moscorun.example.it/ "))
        assertEquals("https://host:8443/sub", MoscoRunServer.normalizeBaseUrl("https://host:8443/sub/"))
        assertEquals("http://127.0.0.1:5080", MoscoRunServer.normalizeBaseUrl("http://127.0.0.1:5080"))
        listOf("http://moscorun.example.it", "https://u:p@host", "https://host?x=1", "ftp://host", "nonsense").forEach {
            assertThrows(IllegalArgumentException::class.java) { MoscoRunServer.normalizeBaseUrl(it) }
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
    }
}

/**
 * Minimal HTTP/1.1 server on a loopback socket: Android unit tests compile against android.jar,
 * which does not expose com.sun.net.httpserver.
 */
private class LoopbackServer(private val respond: (headers: Map<String, String>, body: ByteArray) -> Pair<Int, String>) : AutoCloseable {
    private val socket = ServerSocket(0, 50, InetAddress.getLoopbackAddress())
    val port: Int get() = socket.localPort
    private val worker = thread(isDaemon = true) {
        while (!socket.isClosed) {
            val client = try { socket.accept() } catch (_: IOException) { break }
            client.use { handle(it.getInputStream(), it.getOutputStream()) }
        }
    }

    private fun handle(input: InputStream, output: OutputStream) {
        val reader = BufferedInputStream(input)
        fun line(): String {
            val bytes = ByteArrayOutputStream()
            while (true) {
                val b = reader.read()
                if (b < 0 || b == '\n'.code) break
                if (b != '\r'.code) bytes.write(b)
            }
            return bytes.toString(Charsets.US_ASCII.name())
        }
        line()
        val headers = generateSequence { line().takeIf { it.isNotEmpty() } }
            .associate { it.substringBefore(':').trim().lowercase() to it.substringAfter(':').trim() }
        val body = ByteArrayOutputStream()
        if (headers["transfer-encoding"].equals("chunked", ignoreCase = true)) {
            while (true) {
                val size = line().substringBefore(';').trim().toInt(16)
                if (size == 0) { line(); break }
                body.write(reader.readNBytes(size)); line()
            }
        } else {
            body.write(reader.readNBytes(headers["content-length"]?.toInt() ?: 0))
        }
        val (status, text) = respond(headers, body.toByteArray())
        val bytes = text.toByteArray()
        output.write("HTTP/1.1 $status X\r\nContent-Type: application/json\r\nContent-Length: ${bytes.size}\r\nConnection: close\r\n\r\n".toByteArray())
        output.write(bytes)
        output.flush()
    }

    override fun close() { socket.close(); worker.join(2000) }
}

class MoscoRunServerTest {
    private var status = 201
    private var responseBody = "{}"
    private var received: String? = null
    private var headers: Map<String, String> = emptyMap()
    private lateinit var http: LoopbackServer

    @Before fun start() {
        http = LoopbackServer { h, body ->
            headers = h
            received = GZIPInputStream(body.inputStream()).readBytes().decodeToString()
            status to responseBody
        }
    }

    @After fun stop() = http.close()

    private fun server() = MoscoRunServer("http://127.0.0.1:${http.port}", "mr1_0123456789abcdef_secretsecret")

    @Test fun `sends gzip json with bearer token`() {
        val result = server().upload("""{"a":1}""")

        assertEquals(UploadOutcome.CREATED, result.outcome)
        assertEquals("""{"a":1}""", received)
        assertEquals("Bearer mr1_0123456789abcdef_secretsecret", headers["authorization"])
        assertEquals("gzip", headers["content-encoding"])
    }

    @Test fun `maps server answers to outcomes`() {
        mapOf(200 to UploadOutcome.STORED, 409 to UploadOutcome.CONFLICT, 400 to UploadOutcome.INVALID,
            401 to UploadOutcome.UNAUTHORIZED, 500 to UploadOutcome.FAILED).forEach { (code, outcome) ->
            status = code
            responseBody = """{"title":"t$code"}"""
            assertEquals("HTTP $code", outcome, server().upload("{}").outcome)
        }
    }

    @Test fun `unreachable server is a failure, not an exception`() {
        val port = http.port
        http.close()
        val result = MoscoRunServer("http://127.0.0.1:$port", "mr1_0123456789abcdef_secretsecret", connectTimeoutMs = 2000).upload("{}")
        assertEquals(UploadOutcome.FAILED, result.outcome)
    }
}
