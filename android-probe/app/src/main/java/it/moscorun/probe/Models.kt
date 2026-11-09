package it.moscorun.probe

import com.google.gson.*
import java.time.*
import java.lang.reflect.Type

object Json {
    private class TimeAdapter<T> : JsonSerializer<T> {
        override fun serialize(src: T?, type: Type?, context: JsonSerializationContext?) = JsonPrimitive(src.toString())
    }
    val gson: Gson = GsonBuilder().serializeNulls().setPrettyPrinting()
        .registerTypeAdapter(Instant::class.java, TimeAdapter<Instant>())
        .registerTypeAdapter(Duration::class.java, TimeAdapter<Duration>())
        .registerTypeAdapter(ZoneOffset::class.java, TimeAdapter<ZoneOffset>())
        .registerTypeAdapter(LocalDateTime::class.java, TimeAdapter<LocalDateTime>())
        .registerTypeAdapter(LocalDate::class.java, TimeAdapter<LocalDate>()).create()
    fun tree(value: Any?): JsonElement = gson.toJsonTree(value)
}

data class CategoryResult(
    val name: String, val status: String, val records: Int = 0,
    val samples: Int? = null, val sources: Map<String, Int> = emptyMap(),
    val preview: List<JsonElement> = emptyList(), val message: String? = null
)
data class TrackPoint(val time: String, val latitude: Double, val longitude: Double,
    val altitudeMeters: Double? = null, val accuracyMeters: Double? = null)
data class Sample(val time: String, val value: Double, val unit: String,
    val recordId: String, val source: String)
data class Series(val name: String, val status: String, val samples: List<Sample> = emptyList(),
    val message: String? = null, val rawRecords: List<JsonElement> = emptyList())
data class Workout(val id: String, val title: String, val start: String, val end: String,
    val source: String, val raw: JsonElement, var routeState: String = "non_verificato",
    var route: List<TrackPoint> = emptyList(), var series: List<Series> = emptyList(),
    var detailLoaded: Boolean = false)
data class ProbeReport(val schemaVersion: Int = 1, val provider: String, val generatedAt: String,
    val windowStart: String, val windowEnd: String, val categories: List<CategoryResult>,
    val workouts: List<Workout>, val notes: List<String>)

/** Times stay native: no resampling, interpolation or merging of simultaneous samples. */
object SamplingStats {
    data class Result(val count: Int, val uniqueTimes: Int, val medianIntervalMs: Long?,
        val minIntervalMs: Long?, val maxIntervalMs: Long?)
    fun calculate(times: List<String>): Result {
        val sorted = times.map { Instant.parse(it) }.distinct().sorted()
        val gaps = sorted.zipWithNext { a,b -> Duration.between(a,b).toMillis() }.sorted()
        val median = if (gaps.isEmpty()) null else if (gaps.size % 2 == 1) gaps[gaps.size/2]
            else gaps[gaps.size/2-1] + (gaps[gaps.size/2]-gaps[gaps.size/2-1])/2
        return Result(times.size, sorted.size, median, gaps.firstOrNull(), gaps.lastOrNull())
    }
}
