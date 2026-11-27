package it.moscorun.app.science

import java.time.Instant

/** Epistemic origin of a value, with the server code of `ObservationMethod`. */
enum class ScienceMethod(val code: String) {
    MEASURED("Measured"),
    PROVIDER_REPORTED("ProviderReported"),
    PROVIDER_ESTIMATED("ProviderEstimated"),
}

/** What the phone can read for the scientific part, each with the words shown to the user. */
enum class ScienceMetric(val label: String) {
    RESTING_HEART_RATE("frequenza cardiaca a riposo"),
    HRV_RMSSD("variabilità cardiaca (HRV)"),
    SLEEP("sonno"),
    OXYGEN_SATURATION("saturazione di ossigeno"),
    RESPIRATORY_RATE("frequenza respiratoria"),
    VO2_MAX("VO2max"),
    HEART_RATE("frequenza cardiaca"),
    WEIGHT("peso"),
    BODY_FAT("grasso corporeo"),
    LEAN_MASS("massa magra"),
    BODY_WATER("acqua corporea"),
    BONE_MASS("massa ossea"),
    BMR("metabolismo basale"),
    HEIGHT("altezza"),
    BLOOD_PRESSURE("pressione arteriosa"),
    BODY_TEMPERATURE("temperatura corporea"),
    BLOOD_GLUCOSE("glicemia"),
    STEPS("passi"),
    DISTANCE("distanza"),
    FLOORS("piani saliti"),
    TOTAL_ENERGY("dispendio energetico"),
    ACTIVE_ENERGY("energia attiva"),
    NUTRITION("nutrizione"),
    HYDRATION("idratazione"),
    SAMSUNG_BODY("composizione corporea Samsung"),
    SAMSUNG_HEART("battito Samsung"),
    SAMSUNG_OXYGEN("ossigeno Samsung"),
    SAMSUNG_SKIN("temperatura cutanea Samsung"),
    SAMSUNG_SLEEP("punteggio sonno Samsung"),
    SAMSUNG_PRESSURE("pressione Samsung"),
    SAMSUNG_NUTRITION("nutrizione Samsung"),
    SAMSUNG_WATER("acqua Samsung"),
    SAMSUNG_GLUCOSE("glicemia Samsung"),
    SAMSUNG_TEMP("temperatura corporea Samsung"),
    SAMSUNG_FLOORS("piani saliti Samsung"),
    SAMSUNG_ENERGY("Energy Score Samsung"),
}

/**
 * One value ready for the server, in the canonical unit of the scientific catalog. The native triple keeps what
 * the source reported, so a change of mapping can always be traced back.
 */
data class ScienceObservation(
    val externalId: String,
    val revision: Long,
    val metric: String,
    val unit: String,
    val value: Double?,
    val time: Instant,
    val method: ScienceMethod,
    val providerAlgorithm: String?,
    val nativeMetric: String,
    val nativeUnit: String,
    val nativeValue: Double?,
    val mappingVersion: String = HealthConnectScienceMapping.MAPPING_VERSION,
)

/** What the reader may read now: an empty set means the scientific part waits, without blocking the workouts. */
data class ScienceAvailability(
    val metrics: Set<ScienceMetric>,
    /** True when data older than the first consent may be read. */
    val history: Boolean = false,
    /** Why something is missing, for the status of the run; null when everything is granted. */
    val message: String? = null,
    /** False when the SDK or permission query is unavailable; absence is not a denial. */
    val permissionsKnown: Boolean = true,
)

/**
 * A place daily scientific observations are read from. Implementations only read and never start an activity,
 * so they can run in the background.
 */
interface ScienceReader {
    /** The `source` stored by the server. */
    val source: String

    /** Explicit capability of this reader, not a hardware claim. */
    val supportedMetrics: Set<ScienceMetric> get() = ScienceMetric.entries.filterNot { it.name.startsWith("SAMSUNG_") }.toSet()

    /** True for multi-family SDKs whose permissions and progress must stay independent. */
    val isolatedStreams: Boolean get() = false

    /** Local cursor identity; source on the wire stays unchanged. */
    fun checkpoint(availability: ScienceAvailability): String = source

    suspend fun availability(): ScienceAvailability

    /** Observations whose time is in [from, to), for the metrics in [metrics]. */
    suspend fun read(from: Instant, to: Instant, metrics: Set<ScienceMetric>): List<ScienceObservation>
}
