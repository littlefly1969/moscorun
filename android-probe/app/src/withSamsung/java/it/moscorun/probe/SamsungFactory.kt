package it.moscorun.probe

import android.app.Activity
import com.samsung.android.sdk.health.data.HealthDataService
import com.samsung.android.sdk.health.data.data.HealthDataPoint
import com.samsung.android.sdk.health.data.data.Field
import com.samsung.android.sdk.health.data.permission.AccessType
import com.samsung.android.sdk.health.data.permission.Permission
import com.samsung.android.sdk.health.data.request.*
import kotlinx.coroutines.CancellationException
import java.time.Instant

fun samsungGateway(activity: Activity): SamsungGateway = DirectSamsungProbe(activity)

/** Compiled only with the licensed, official Samsung Health Data SDK AAR. */
private class DirectSamsungProbe(activity: Activity): SamsungGateway {
    private val appContext = activity.applicationContext
    private val store by lazy { HealthDataService.getStore(appContext) }
    override val available = true
    override val setupMessage = "Accesso diretto Samsung: per questa prova di sviluppo servono Samsung Health compatibile e la modalità sviluppatore Samsung Health Data SDK per la lettura. Poi concedi i permessi a MoscoRun. Per la distribuzione occorre la registrazione partner."
    private data class Spec(val name:String, val type:DataType,
        val builder:()->ReadDataRequest.DualTimeBuilder<HealthDataPoint>)
    private val specs = listOf(
        Spec("Allenamenti",DataTypes.EXERCISE) { DataTypes.EXERCISE.readDataRequestBuilder },
        Spec("Frequenza cardiaca",DataTypes.HEART_RATE) { DataTypes.HEART_RATE.readDataRequestBuilder },
        Spec("Peso e composizione",DataTypes.BODY_COMPOSITION) { DataTypes.BODY_COMPOSITION.readDataRequestBuilder },
        Spec("Sonno",DataTypes.SLEEP) { DataTypes.SLEEP.readDataRequestBuilder },
        Spec("Alimentazione",DataTypes.NUTRITION) { DataTypes.NUTRITION.readDataRequestBuilder },
        Spec("Idratazione",DataTypes.WATER_INTAKE) { DataTypes.WATER_INTAKE.readDataRequestBuilder }
    )
    private val routePermission get() = Permission.of(DataTypes.EXERCISE_LOCATION,AccessType.READ)
    private val permissions get() = specs.map { Permission.of(it.type,AccessType.READ) }.toSet() + routePermission
    override suspend fun requestPermissions(activity:Activity):String {
        val granted=store.requestPermissions(permissions,activity)
        return "Permessi Samsung concessi: ${granted.size}/${permissions.size}."
    }
    override suspend fun scan(start:Instant,end:Instant):ProbeReport {
        val allowed=store.getGrantedPermissions(permissions)
        val workouts=mutableListOf<Workout>()
        val categories=specs.map { spec ->
            if(Permission.of(spec.type,AccessType.READ) !in allowed) CategoryResult(spec.name,"permesso_negato")
            else try {
                var token:String?=null; val seen=mutableSetOf<String>(); var count=0; var complete=false
                val previews=mutableListOf<com.google.gson.JsonElement>(); val sources=mutableMapOf<String,Int>()
                for(page in 0 until 200) {
                    val request=spec.builder().setInstantTimeFilter(InstantTimeFilter.of(start,end))
                        .setPageSize(500).setPageToken(token).setOrdering(Ordering.ASC).build()
                    val response=store.readData(request)
                    response.dataList.forEach { point ->
                        count++
                        val origin=point.dataSource
                        val source="${origin?.appId ?: "?"} / dispositivo ${origin?.deviceId ?: "?"}"
                        sources[source]=(sources[source] ?: 0)+1
                        if(previews.size<3) previews+=samsungSnapshot(point,spec.type)
                        if(spec.type==DataTypes.EXERCISE && workouts.size<300) {
                            val sessions=point.getValue(DataType.ExerciseType.SESSIONS).orEmpty()
                            sessions.forEachIndexed { index, session ->
                                val logs=session.log.orEmpty()
                                fun metric(name:String,unit:String,value:(com.samsung.android.sdk.health.data.data.entries.ExerciseLog)->Float?):Series {
                                    val samples=logs.mapNotNull { log -> value(log)?.let { v ->
                                        Sample(log.timestamp.toString(),v.toDouble(),unit,point.uid,source)
                                    } }
                                    return Series(name,"letto",samples,"Campioni appartenenti al log della sessione Samsung.")
                                }
                                val points=samsungRoutePoints(session.route.orEmpty())
                                workouts+=Workout("${point.uid}:$index",session.exerciseType.toString(),
                                    session.startTime.toString(),session.endTime.toString(),source,Json.tree(mapOf(
                                        "recordId" to point.uid,"clientDataId" to point.clientDataId,
                                        "clientVersion" to point.clientVersion,"updateTime" to point.updateTime,
                                        "dataSource" to samsungSource(point.dataSource),"zoneOffset" to point.zoneOffset,
                                        "sessionIndex" to index,"session" to session)),
                                    if(routePermission !in allowed) "permesso_negato" else if(points.isEmpty()) "assente" else "disponibile",points,
                                    listOf(metric("Frequenza cardiaca","bpm") { it.heartRate },metric("Velocità","m/s") { it.speed },
                                        metric("Cadenza","passi/min") { it.cadence },metric("Potenza","W") { it.power }),true)
                            }
                        }
                    }
                    token=response.pageToken?.takeIf { it.isNotEmpty() }
                    if(token==null) { complete=true;break }
                    check(seen.add(token!!)) { "Token di paginazione ripetuto" }
                }
                CategoryResult(spec.name,if(complete) "letto" else "parziale",count,sources=sources,preview=previews,
                    message=if(complete) null else "Limite di 100.000 record: ridurre l'intervallo.")
            } catch(e:CancellationException) { throw e }
              catch(e:Exception) { CategoryResult(spec.name,"errore",message=e.message ?: e.javaClass.simpleName) }
        }
        return ProbeReport(provider="Samsung Health Data SDK",generatedAt=Instant.now().toString(),windowStart=start.toString(),windowEnd=end.toString(),
            categories=categories,workouts=workouts.sortedByDescending { it.start },notes=listOf(
                "Prova locale di sola lettura. Anteprime limitate a 3 record per categoria; elenco fino a 300 record di allenamento.",
                "Le serie nel dettaglio provengono dal log originale Samsung. Il contenuto completo della sessione è nel campo raw.",
                "Per alcuni tipi esistono soltanto aggregazioni: questa versione verifica le categorie elencate, non ogni API Samsung.",
                "SDK e autorizzazione Samsung vanno verificati sul telefono reale. Nessun dato è inviato al server."
            ))
    }
}

@Suppress("UNCHECKED_CAST")
internal fun samsungSnapshot(point:HealthDataPoint, type:DataType):com.google.gson.JsonElement {
        val values=linkedMapOf<String,Any?>()
        // SDK 1.1.0 exposes its descriptors as public static @JvmField constants.
        type.javaClass.fields.filter {
            java.lang.reflect.Modifier.isStatic(it.modifiers) && Field::class.java.isAssignableFrom(it.type)
        }.forEach { field ->
            val sdkField=field.get(null) as Field<Any>
            values[field.name]=point.getValue(sdkField)
        }
        return Json.tree(mapOf("id" to point.uid,"start" to point.startTime,"end" to point.endTime,
            "source" to samsungSource(point.dataSource),"zoneOffset" to point.zoneOffset,
            "clientDataId" to point.clientDataId,"clientVersion" to point.clientVersion,
            "updateTime" to point.updateTime,"fields" to values))
    }

internal fun samsungRoutePoints(points:List<com.samsung.android.sdk.health.data.data.entries.ExerciseLocation>) =
    points.map { TrackPoint(it.timestamp.toString(),it.latitude.toDouble(),it.longitude.toDouble(),it.altitude?.toDouble(),it.accuracy?.toDouble()) }

internal fun samsungSource(source:com.samsung.android.sdk.health.data.data.DataSource?) =
    source?.let { mapOf("appId" to it.appId,"deviceId" to it.deviceId) }
