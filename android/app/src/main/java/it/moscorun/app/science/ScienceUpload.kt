package it.moscorun.app.science

import com.google.gson.GsonBuilder

/** Body of POST /api/v1/ingest/observations (contracts/moscorun-api.json, IngestObservationsRequest). */
data class ObservationNative(val metric: String, val unit: String, val value: Double?, val mappingVersion: String)

data class ObservationItem(
    val externalId: String,
    val revision: String,
    val revisionOrder: Long,
    val metric: String,
    val unit: String,
    val value: Double?,
    val time: String,
    val method: String,
    val providerAlgorithm: String?,
    val native: ObservationNative,
)

data class ObservationBatch(val source: String, val samples: List<ObservationItem>)

object ScienceUpload {
    /** Server limit of observations per request. */
    const val MAX_BATCH = 500

    private val compact = GsonBuilder().disableHtmlEscaping().create()

    fun item(o: ScienceObservation) = ObservationItem(o.externalId, o.revision.toString(), o.revision, o.metric, o.unit,
        o.value, o.time.toString(), o.method.code, o.providerAlgorithm,
        ObservationNative(o.nativeMetric, o.nativeUnit, o.nativeValue, HealthConnectScienceMapping.MAPPING_VERSION))

    fun json(source: String, observations: List<ScienceObservation>): String {
        require(observations.size in 1..MAX_BATCH) { "Lotto di osservazioni non valido." }
        return compact.toJson(ObservationBatch(source, observations.map(::item)))
    }
}

enum class ScienceOutcome {
    /** 200: stored, or already present with the same content. */
    STORED,

    /** 400: the server rejects this content; sending it again cannot succeed. */
    INVALID,

    /** 409: the same revision is on the server with a different content. */
    CONFLICT,

    /** 401/403: the token is no longer accepted, or is not a phone token. */
    UNAUTHORIZED,

    /** 404: a server older than the scientific ingestion. */
    UNSUPPORTED,

    /** 429, 5xx, timeouts and network errors: worth trying again later. */
    RETRY,
}

data class ScienceUploadResult(val outcome: ScienceOutcome, val detail: String? = null, val imported: Int = 0)

/** The server operation the scientific part needs; servers without it answer as [ScienceOutcome.UNSUPPORTED]. */
interface ScienceServer {
    fun postObservations(json: String): ScienceUploadResult = ScienceUploadResult(ScienceOutcome.UNSUPPORTED)
}
