package it.moscorun.probe

import com.google.gson.GsonBuilder
import com.google.gson.JsonParser
import java.io.IOException
import java.net.HttpURLConnection
import java.net.URI
import java.net.URL
import java.security.MessageDigest
import java.util.zip.GZIPOutputStream

/** Body of POST /api/v1/ingest/workouts (contracts/moscorun-api.json, IngestWorkoutRequest). */
data class UploadPoint(val time: String, val lat: Double, val lon: Double, val altitudeM: Double?, val accuracyM: Double?)
data class UploadSample(val time: String, val value: Double)
data class UploadSeries(val metric: String, val samples: List<UploadSample>)
data class UploadPayload(val source: String, val sourceId: String, val revision: Long, val activityType: String,
    val startedAt: String, val endedAt: String, val utcOffsetMinutes: Int?, val activeDurationSeconds: Double?,
    val distanceM: Double?, val energyKcal: Double?, val averageHeartRate: Double?, val maxHeartRate: Double?,
    val track: List<UploadPoint>, val series: List<UploadSeries>)

object UploadMapper {
    private val idChars = Regex("^[A-Za-z0-9_.:-]{1,128}$")
    private val ranges = mapOf("HeartRate" to 20.0..260.0, "Speed" to 0.0..100.0, "Cadence" to 0.0..400.0, "Power" to 0.0..3000.0)
    private val compact = GsonBuilder().disableHtmlEscaping().create()

    /** Activity codes keep the source value; characters outside the server alphabet become '_'. */
    fun activityCode(raw: String) = raw.replace(Regex("[^A-Za-z0-9_.:-]"), "_").take(64).ifEmpty { "UNKNOWN" }

    /**
     * Converts a workout read on the phone into the server payload, or null when the source cannot be uploaded.
     * Samples outside the physical range of their metric (e.g. heart rate 0 written by the watch while the sensor
     * had no contact) are gaps, not measurements: they are dropped instead of rejecting the whole workout.
     */
    fun toPayload(w: Workout): UploadPayload? {
        val summary = w.summary ?: return null
        val source = w.uploadSource ?: return null
        if (!idChars.matches(w.id)) return null
        val series = w.series.mapNotNull { s ->
            val metric = s.metric ?: return@mapNotNull null
            val range = ranges[metric] ?: return@mapNotNull null
            UploadSeries(metric, s.samples.filter { it.value.isFinite() && it.value in range }.map { UploadSample(it.time, it.value) })
                .takeIf { it.samples.isNotEmpty() }
        }.distinctBy { it.metric }
        val sessionSeconds = java.time.Duration.between(java.time.Instant.parse(w.start), java.time.Instant.parse(w.end)).toMillis() / 1000.0
        val heartRate = ranges.getValue("HeartRate")
        // Summary values outside the server range (e.g. 0 bpm when the watch did not measure) are missing, not data.
        return UploadPayload(source, w.id, summary.revision.coerceAtLeast(0), activityCode(summary.activityType), w.start, w.end,
            summary.utcOffsetMinutes?.takeIf { it in -840..840 },
            summary.activeDurationSeconds?.takeIf { it.isFinite() && it >= 0 && it <= sessionSeconds + 1 },
            summary.distanceM?.takeIf { it.isFinite() && it in 0.0..1_000_000.0 },
            summary.energyKcal?.takeIf { it.isFinite() && it in 0.0..50_000.0 },
            summary.averageHeartRate?.takeIf { it in heartRate }, summary.maxHeartRate?.takeIf { it in heartRate },
            w.route.filter { it.latitude.isFinite() && it.longitude.isFinite() }
                .map { UploadPoint(it.time, it.latitude, it.longitude, it.altitudeMeters?.takeIf(Double::isFinite),
                    it.accuracyMeters?.takeIf(Double::isFinite)) },
            series)
    }

    fun json(payload: UploadPayload): String = compact.toJson(payload)

    /** Fingerprint of what was sent: unchanged workouts are not uploaded again. */
    fun fingerprint(json: String): String =
        MessageDigest.getInstance("SHA-256").digest(json.toByteArray()).joinToString("") { "%02x".format(it) }
}

enum class UploadOutcome { CREATED, STORED, CONFLICT, INVALID, UNAUTHORIZED, FAILED }
data class UploadResult(val outcome: UploadOutcome, val detail: String? = null)

/** Minimal HTTPS client for a self-hosted MoscoRun server, authenticated with a device token. */
class MoscoRunServer(baseUrl: String, private val token: String,
    private val connectTimeoutMs: Int = 15_000, private val readTimeoutMs: Int = 60_000) {
    private val base = normalizeBaseUrl(baseUrl)
    private val endpoint: URL = URI("$base/api/v1/ingest/workouts").toURL()

    init { require(validToken(token)) { "Token del dispositivo non valido." } }

    fun upload(json: String): UploadResult {
        val connection = endpoint.openConnection() as HttpURLConnection
        return try {
            connection.requestMethod = "POST"
            connection.connectTimeout = connectTimeoutMs
            connection.readTimeout = readTimeoutMs
            connection.doOutput = true
            connection.instanceFollowRedirects = false
            connection.setRequestProperty("Authorization", "Bearer $token")
            connection.setRequestProperty("Content-Type", "application/json")
            connection.setRequestProperty("Content-Encoding", "gzip")
            connection.setRequestProperty("Accept", "application/json")
            connection.setChunkedStreamingMode(0)
            GZIPOutputStream(connection.outputStream).use { it.write(json.toByteArray()) }
            val code = connection.responseCode
            val body = (if (code < 400) connection.inputStream else connection.errorStream)?.use { it.readBytes().decodeToString() }
            when (code) {
                201 -> UploadResult(UploadOutcome.CREATED)
                200 -> UploadResult(UploadOutcome.STORED)
                409 -> UploadResult(UploadOutcome.CONFLICT, problemDetail(body))
                400 -> UploadResult(UploadOutcome.INVALID, problemDetail(body))
                401, 403 -> UploadResult(UploadOutcome.UNAUTHORIZED, "Token non valido o revocato: registrare di nuovo il telefono.")
                else -> UploadResult(UploadOutcome.FAILED, "Risposta HTTP $code")
            }
        } catch (e: java.net.HttpRetryException) {
            // Streaming bodies cannot be resent: the JDK reports an authentication challenge this way.
            if (e.responseCode() == 401) UploadResult(UploadOutcome.UNAUTHORIZED, "Token non valido o revocato: registrare di nuovo il telefono.")
            else UploadResult(UploadOutcome.FAILED, e.message ?: "HTTP ${e.responseCode()}")
        } catch (e: IOException) {
            UploadResult(UploadOutcome.FAILED, e.message ?: e.javaClass.simpleName)
        } finally {
            connection.disconnect()
        }
    }

    /** Workouts stored on the server in its default window (last 90 days), newest first. */
    fun workouts(): ReadResult<List<RemoteWorkout>> = get("/api/v1/workouts", ServerReadParser::workouts)

    /** Route as seen by the server, after its privacy zones; at most [maxPoints] points. */
    fun route(workoutId: String, maxPoints: Int = 5000): ReadResult<RemoteRoute> {
        require(ServerReadParser.validId(workoutId)) { "Identificativo non valido." }
        return get("/api/v1/workouts/$workoutId/route?maxPoints=$maxPoints", ServerReadParser::route)
    }

    private fun <T> get(path: String, parse: (String) -> T): ReadResult<T> {
        val connection = URI(base + path).toURL().openConnection() as HttpURLConnection
        return try {
            connection.connectTimeout = connectTimeoutMs
            connection.readTimeout = readTimeoutMs
            connection.instanceFollowRedirects = false
            connection.setRequestProperty("Authorization", "Bearer $token")
            connection.setRequestProperty("Accept", "application/json")
            val code = connection.responseCode
            val body = (if (code < 400) connection.inputStream else connection.errorStream)?.use { it.readBytes().decodeToString() }
            when (code) {
                200 -> try { ReadResult.Ok(parse(body.orEmpty())) }
                    catch (e: RuntimeException) { ReadResult.Failed("Risposta del server non riconosciuta: ${e.message ?: e.javaClass.simpleName}") }
                401 -> ReadResult.Failed("Token non valido o revocato: registrare di nuovo il telefono.", unauthorized = true)
                403 -> ReadResult.Failed("Il token non permette la lettura: registrare il telefono come \"Telefono Android\".", unauthorized = true)
                404 -> ReadResult.Failed("Non disponibile sul server (o nascosto dalle zone private).")
                else -> ReadResult.Failed(problemDetail(body) ?: "Risposta HTTP $code")
            }
        } catch (e: IOException) {
            ReadResult.Failed(e.message ?: e.javaClass.simpleName)
        } finally {
            connection.disconnect()
        }
    }

    companion object {
        private val tokenPattern = Regex("^mr1_[A-Za-z0-9_-]{10,200}$")

        fun validToken(token: String) = tokenPattern.matches(token)

        /**
         * Accepts only HTTPS addresses without credentials, query or fragment: the token must never travel in clear.
         * Plain HTTP is allowed only for loopback (development against a local server).
         */
        fun normalizeBaseUrl(raw: String): String {
            val uri = URI(raw.trim())
            val host = uri.host ?: throw IllegalArgumentException("Indirizzo del server non valido.")
            val loopback = host == "localhost" || host == "127.0.0.1" || host == "10.0.2.2"
            require(uri.scheme == "https" || (uri.scheme == "http" && loopback)) { "Il server deve usare HTTPS." }
            require(uri.userInfo == null && uri.query == null && uri.fragment == null) { "L'indirizzo non deve contenere credenziali o parametri." }
            return "${uri.scheme}://${uri.rawAuthority}${uri.rawPath.orEmpty().trimEnd('/')}"
        }

        internal fun problemDetail(body: String?): String? = try {
            val problem = JsonParser.parseString(body ?: return null).asJsonObject
            val errors = problem.getAsJsonObject("errors")?.entrySet()?.joinToString("; ") { (field, messages) ->
                "$field: ${messages.asJsonArray.joinToString(", ") { it.asString }}"
            }
            errors ?: problem.get("detail")?.asString ?: problem.get("title")?.asString
        } catch (_: Exception) { null }
    }
}
