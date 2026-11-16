package it.moscorun.app.server

import com.google.gson.JsonParser
import java.io.IOException
import java.net.HttpURLConnection
import java.net.URI
import java.net.URLEncoder
import java.time.Instant
import java.util.zip.GZIPOutputStream

/** What the sync engine needs from the server; the HTTPS client below is the only real implementation. */
interface SyncServer {
    fun upload(json: String): UploadResult

    /** Summaries of the workouts starting in the interval, any source. */
    fun workoutsBetween(from: Instant, to: Instant): ReadResult<List<RemoteWorkout>>
}

/** The server operations a run needs beyond uploading. */
interface ServerApi : SyncServer {
    /** Checks address and token without changing anything. Servers before this endpoint answer 404. */
    fun identity(): ReadResult<DeviceIdentity>
}

/** Minimal HTTPS client for a self-hosted MoscoRun server, authenticated with a device token. */
class MoscoRunServer(baseUrl: String, private val token: String,
    private val connectTimeoutMs: Int = 15_000, private val readTimeoutMs: Int = 60_000) : ServerApi {
    private val base = normalizeBaseUrl(baseUrl)

    init { require(validToken(token)) { "Token del dispositivo non valido." } }

    override fun upload(json: String): UploadResult {
        val connection = URI("$base/api/v1/ingest/workouts").toURL().openConnection() as HttpURLConnection
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
                201 -> UploadResult(UploadOutcome.CREATED, serverId = storedId(body))
                200 -> UploadResult(UploadOutcome.STORED, serverId = storedId(body))
                409 -> UploadResult(UploadOutcome.CONFLICT, problemDetail(body) ?: "Conflitto di revisione")
                400 -> UploadResult(UploadOutcome.INVALID, problemDetail(body) ?: "Contenuto non accettato")
                413 -> UploadResult(UploadOutcome.TOO_LARGE, "Allenamento troppo grande per il server.")
                401, 403 -> UploadResult(UploadOutcome.UNAUTHORIZED, UNAUTHORIZED_MESSAGE)
                else -> UploadResult(UploadOutcome.RETRY, "Risposta HTTP $code")
            }
        } catch (e: java.net.HttpRetryException) {
            // Streaming bodies cannot be resent: the JDK reports an authentication challenge this way.
            if (e.responseCode() == 401) UploadResult(UploadOutcome.UNAUTHORIZED, UNAUTHORIZED_MESSAGE)
            else UploadResult(UploadOutcome.RETRY, "Risposta HTTP ${e.responseCode()}")
        } catch (e: IOException) {
            // The message of a network error names hosts and ports, never health data.
            UploadResult(UploadOutcome.RETRY, e.message ?: e.javaClass.simpleName)
        } finally {
            connection.disconnect()
        }
    }

    /**
     * One page of the server list, newest first. Servers older than the paginated API ignore the parameters and
     * answer with their fixed window and no cursor: the caller then simply sees a single page.
     */
    fun workouts(pageSize: Int = 50, cursor: String? = null, from: Instant? = null, to: Instant? = null): ReadResult<WorkoutPage> {
        require(pageSize in 1..200) { "pageSize fuori intervallo." }
        val query = buildString {
            append("pageSize=").append(pageSize)
            cursor?.let { append("&cursor=").append(encode(it)) }
            from?.let { append("&from=").append(encode(it.toString())) }
            to?.let { append("&to=").append(encode(it.toString())) }
        }
        return get("/api/v1/workouts?$query", ServerReadParser::page)
    }

    override fun workoutsBetween(from: Instant, to: Instant): ReadResult<List<RemoteWorkout>> {
        val all = mutableListOf<RemoteWorkout>()
        var cursor: String? = null
        repeat(MAX_PAGES) {
            when (val page = workouts(200, cursor, from, to)) {
                is ReadResult.Failed -> return page
                is ReadResult.Ok -> {
                    all += page.value.items
                    cursor = page.value.nextCursor ?: return ReadResult.Ok(all)
                }
            }
        }
        return ReadResult.Failed("Troppi allenamenti nell'intervallo richiesto.")
    }

    fun detail(workoutId: String): ReadResult<RemoteDetail> {
        require(ServerReadParser.validId(workoutId)) { "Identificativo non valido." }
        return get("/api/v1/workouts/$workoutId", ServerReadParser::detail)
    }

    /** One metric, reduced by the server to at most [maxPoints] samples that keep real minima and maxima. */
    fun series(workoutId: String, metric: String, maxPoints: Int = 600): ReadResult<RemoteSeries?> {
        require(ServerReadParser.validId(workoutId)) { "Identificativo non valido." }
        require(ServerReadParser.validMetric(metric)) { "Grandezza non valida." }
        return get("/api/v1/workouts/$workoutId/series?metric=$metric&maxPoints=$maxPoints") {
            ServerReadParser.series(it, metric).firstOrNull()
        }
    }

    /** Route as seen by the server, after its privacy zones; at most [maxPoints] points. */
    fun route(workoutId: String, maxPoints: Int = 5000): ReadResult<RemoteRoute> {
        require(ServerReadParser.validId(workoutId)) { "Identificativo non valido." }
        return get("/api/v1/workouts/$workoutId/route?maxPoints=$maxPoints", ServerReadParser::route)
    }

    override fun identity(): ReadResult<DeviceIdentity> = get("/api/v1/device", ServerReadParser::identity)

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
            when {
                code == 200 -> try { ReadResult.Ok(parse(body.orEmpty())) }
                    catch (e: RuntimeException) { ReadResult.Failed("Risposta del server non riconosciuta: ${e.message ?: e.javaClass.simpleName}") }
                code == 401 -> ReadResult.Failed(UNAUTHORIZED_MESSAGE, Failure.UNAUTHORIZED)
                code == 403 -> ReadResult.Failed("Il token non permette la lettura: registrare il telefono come \"Telefono Android\".", Failure.FORBIDDEN)
                code == 404 -> ReadResult.Failed("Non disponibile sul server (o nascosto dalle zone private).", Failure.NOT_FOUND)
                code == 429 || code >= 500 -> ReadResult.Failed("Il server non è disponibile ora (HTTP $code).", Failure.UNAVAILABLE)
                else -> ReadResult.Failed(problemDetail(body) ?: "Risposta HTTP $code")
            }
        } catch (e: IOException) {
            ReadResult.Failed("Server non raggiungibile: ${e.message ?: e.javaClass.simpleName}", Failure.OFFLINE)
        } finally {
            connection.disconnect()
        }
    }

    companion object {
        private const val MAX_PAGES = 50
        private const val UNAUTHORIZED_MESSAGE = "Token non valido o revocato: sostituirlo nelle impostazioni."
        private val tokenPattern = Regex("^mr1_[A-Za-z0-9_-]{10,200}$")

        fun validToken(token: String) = tokenPattern.matches(token)

        /**
         * Accepts only HTTPS addresses without credentials, query or fragment: the token must never travel in clear.
         * Plain HTTP is allowed only for loopback (development against a local server).
         */
        fun normalizeBaseUrl(raw: String): String {
            val uri = try { URI(raw.trim()) } catch (_: java.net.URISyntaxException) { throw IllegalArgumentException("Indirizzo del server non valido.") }
            val host = uri.host ?: throw IllegalArgumentException("Indirizzo del server non valido.")
            val loopback = host == "localhost" || host == "127.0.0.1" || host == "10.0.2.2"
            require(uri.scheme == "https" || (uri.scheme == "http" && loopback)) { "Il server deve usare HTTPS." }
            require(uri.userInfo == null && uri.query == null && uri.fragment == null) { "L'indirizzo non deve contenere credenziali o parametri." }
            return "${uri.scheme}://${uri.rawAuthority}${uri.rawPath.orEmpty().trimEnd('/')}"
        }

        internal fun problemDetail(body: String?): String? = if (body == null) null else try {
            val problem = JsonParser.parseString(body).asJsonObject
            val errors = problem.getAsJsonObject("errors")?.entrySet()?.joinToString("; ") { (field, messages) ->
                "$field: ${messages.asJsonArray.joinToString(", ") { it.asString }}"
            }
            errors ?: problem.get("detail")?.asString ?: problem.get("title")?.asString
        } catch (_: Exception) { null }

        private fun storedId(body: String?): String? {
            if (body == null) return null
            return try {
                JsonParser.parseString(body).asJsonObject.get("id")?.asString?.takeIf(ServerReadParser::validId)
            } catch (_: Exception) { null }
        }

        private fun encode(value: String) = URLEncoder.encode(value, "UTF-8")
    }
}
