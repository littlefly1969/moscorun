package it.moscorun.probe

import android.content.Context
import androidx.health.connect.client.HealthConnectClient
import androidx.health.connect.client.permission.HealthPermission
import androidx.health.connect.client.records.*
import androidx.health.connect.client.records.metadata.DataOrigin
import androidx.health.connect.client.request.ReadRecordsRequest
import androidx.health.connect.client.time.TimeRangeFilter
import kotlinx.coroutines.CancellationException
import java.time.Instant
import kotlin.reflect.KClass

class HealthConnectProbe(private val context: Context) {
    companion object { const val SAMSUNG_PACKAGE = "com.sec.android.app.shealth" }
    private val client get() = HealthConnectClient.getOrCreate(context)
    val types: List<Pair<String, KClass<out Record>>> = listOf(
        "Allenamenti" to ExerciseSessionRecord::class, "Frequenza cardiaca" to HeartRateRecord::class,
        "Distanza" to DistanceRecord::class, "Velocità" to SpeedRecord::class,
        "Potenza" to PowerRecord::class, "Cadenza passi" to StepsCadenceRecord::class,
        "Passi" to StepsRecord::class, "Peso" to WeightRecord::class,
        "Grasso corporeo" to BodyFatRecord::class, "Sonno" to SleepSessionRecord::class,
        "Alimentazione" to NutritionRecord::class, "Calorie totali" to TotalCaloriesBurnedRecord::class,
        "VO₂max" to Vo2MaxRecord::class
    )
    val permissions: Set<String> get() = types.map { HealthPermission.getReadPermission(it.second) }.toSet()
    fun sdkStatus() = HealthConnectClient.getSdkStatus(context)
    suspend fun granted() = client.permissionController.getGrantedPermissions()
    private fun origins(samsungOnly: Boolean) = if (samsungOnly) setOf(DataOrigin(SAMSUNG_PACKAGE)) else emptySet()

    private suspend fun <T: Record> readPages(type: KClass<T>, start: Instant, end: Instant,
        samsungOnly: Boolean, consume: (List<T>) -> Unit): Boolean {
        var token: String? = null
        val seen = mutableSetOf<String>()
        repeat(200) {
            val page = client.readRecords(ReadRecordsRequest(type,
                TimeRangeFilter.between(start, end), dataOriginFilter = origins(samsungOnly),
                pageSize = 500, pageToken = token))
            consume(page.records)
            token = page.pageToken?.takeIf { it.isNotEmpty() }
            if (token == null) return true
            check(seen.add(token!!)) { "Token di paginazione ripetuto: scansione incompleta." }
        }
        return false
    }
    private fun sampleCount(record: Record): Int = when(record) {
        is HeartRateRecord -> record.samples.size
        is SpeedRecord -> record.samples.size
        is PowerRecord -> record.samples.size
        is StepsCadenceRecord -> record.samples.size
        else -> 1
    }
    suspend fun scan(start: Instant, end: Instant, samsungOnly: Boolean): ProbeReport {
        val allowed = granted()
        val workouts = mutableListOf<Workout>()
        val categories = types.map { (name, type) ->
            if (HealthPermission.getReadPermission(type) !in allowed) CategoryResult(name, "permesso_negato")
            else try {
                var count = 0; var samples = 0
                val sourceCounts = mutableMapOf<String,Int>()
                val preview = mutableListOf<com.google.gson.JsonElement>()
                val complete = readPages(type, start, end, samsungOnly) { rows -> rows.forEach { row ->
                    count++; samples += sampleCount(row)
                    val origin = row.metadata.dataOrigin.packageName
                    sourceCounts[origin] = (sourceCounts[origin] ?: 0) + 1
                    if (preview.size < 3) preview += Json.tree(row)
                    if (row is ExerciseSessionRecord && workouts.size < 300) workouts += workout(row)
                } }
                CategoryResult(name, if (complete) "letto" else "parziale", count, samples, sourceCounts, preview,
                    if (!complete) "Raggiunto il limite di 100.000 record: ridurre l'intervallo." else null)
            } catch (e: CancellationException) { throw e }
              catch (e: Exception) { CategoryResult(name,"errore", message = e.message ?: e.javaClass.simpleName) }
        }
        return ProbeReport(provider="Health Connect", generatedAt=Instant.now().toString(),
            windowStart=start.toString(), windowEnd=end.toString(), categories=categories,
            workouts=workouts.sortedByDescending { it.start }, notes=listOf(
                "Scansione in primo piano, sola lettura. Filtro Samsung: $samsungOnly.",
                "Anteprima: primi 3 record per categoria. Dettagli completi caricati scegliendo un allenamento.",
                "Elenco allenamenti limitato a 300; ridurre l'intervallo se necessario.",
                "Le categorie possono avere frequenze e provenienze diverse; non sono battiti ECG o segnali grezzi.",
                "Percorso e campioni si verificano nel dettaglio. L'assenza in Health Connect non dimostra l'assenza in Samsung Health."
            ))
    }
    private fun workout(r: ExerciseSessionRecord): Workout {
        val route = r.exerciseRouteResult
        return Workout(r.metadata.id, r.title ?: "Attività ${r.exerciseType}", r.startTime.toString(),
            r.endTime.toString(), r.metadata.dataOrigin.packageName, Json.tree(r),
            when(route) { is ExerciseRouteResult.Data -> "disponibile"; is ExerciseRouteResult.ConsentRequired -> "consenso_richiesto"; else -> "assente" },
            if (route is ExerciseRouteResult.Data) routePoints(route.exerciseRoute) else emptyList())
    }
    fun routePoints(route: ExerciseRoute) = route.route.map {
        TrackPoint(it.time.toString(), it.latitude, it.longitude, it.altitude?.inMeters, it.horizontalAccuracy?.inMeters)
    }
    suspend fun details(workout: Workout): Workout {
        val start = Instant.parse(workout.start); val end = Instant.parse(workout.end)
        val allowed = granted()
        val origin = setOf(DataOrigin(workout.source))
        suspend fun <T:Record> series(name: String, type: KClass<T>, values:(T)->List<Sample>): Series {
            if (HealthPermission.getReadPermission(type) !in allowed) return Series(name,"permesso_negato")
            return try {
                val raw = mutableListOf<com.google.gson.JsonElement>(); val samples = mutableListOf<Sample>()
                var token: String? = null; val seen = mutableSetOf<String>(); var complete = false
                for (pageIndex in 0 until 200) {
                    val page = client.readRecords(ReadRecordsRequest(type, TimeRangeFilter.between(start,end),
                        dataOriginFilter=origin, pageSize=500, pageToken=token))
                    for (record in page.records) {
                        // Keep source records and native timestamps; clip only the derived session sample view.
                        raw += Json.tree(record)
                        samples += values(record).filter { !Instant.parse(it.time).isBefore(start) && Instant.parse(it.time).isBefore(end) }
                    }
                    token = page.pageToken?.takeIf { it.isNotEmpty() }
                    if (token == null) { complete = true; break }
                    check(seen.add(token)) { "Token ripetuto" }
                    if (samples.size >= 100_000) break
                }
                Series(name, if(complete) "letto" else "parziale",samples,
                    if(complete) "Associazione per intervallo e app di origine; non prova un collegamento univoco con la corsa." else "Limite di memoria raggiunto; risultato incompleto.",raw)
            } catch(e:CancellationException) { throw e }
              catch(e:Exception) { Series(name,"errore",message=e.message ?: e.javaClass.simpleName) }
        }
        fun sample(t:Instant,v:Double,u:String,r:Record)=Sample(t.toString(),v,u,r.metadata.id,r.metadata.dataOrigin.packageName)
        workout.series=listOf(
            series("Frequenza cardiaca",HeartRateRecord::class) { r -> r.samples.map { sample(it.time,it.beatsPerMinute.toDouble(),"bpm",r) } },
            series("Velocità",SpeedRecord::class) { r -> r.samples.map { sample(it.time,it.speed.inMetersPerSecond,"m/s",r) } },
            series("Potenza",PowerRecord::class) { r -> r.samples.map { sample(it.time,it.power.inWatts,"W",r) } },
            series("Cadenza",StepsCadenceRecord::class) { r -> r.samples.map { sample(it.time,it.rate,"passi/min",r) } },
            series("Distanza (record per intervallo)",DistanceRecord::class) { emptyList() },
            series("Calorie totali (record per intervallo)",TotalCaloriesBurnedRecord::class) { emptyList() },
            series("VO₂max (record originali)",Vo2MaxRecord::class) { emptyList() }
        )
        workout.detailLoaded=true
        return workout
    }
}
