package it.moscorun.app.server

import it.moscorun.app.science.ObservationBatch
import it.moscorun.app.science.ObservationItem
import it.moscorun.app.science.ObservationNative
import it.moscorun.app.science.ScienceDiagnosticReport
import it.moscorun.app.science.ScienceMetricDiagnostic
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test
import java.lang.reflect.Modifier
import java.lang.reflect.ParameterizedType
import java.lang.reflect.Type

/**
 * The release build is shrunk by R8, which keeps only the fields of classes marked [WireFormat]. Every class that
 * Gson reaches from a request body must therefore carry the marker, otherwise its JSON would lose fields in release.
 */
class WireFormatTest {
    private val roots = listOf(UploadPayload::class.java, ObservationBatch::class.java, ScienceDiagnosticReport::class.java)

    @Test
    fun everyAppClassReachableFromARequestBodyIsMarked() {
        val unmarked = reachable().filter { it.name.startsWith("it.moscorun.") && !it.isAnnotationPresent(WireFormat::class.java) }
        assertEquals("Classi serializzate senza @WireFormat: $unmarked", emptyList<Class<*>>(), unmarked)
    }

    @Test
    fun theWalkFollowsListsDownToTheLeaves() {
        assertTrue(reachable().containsAll(listOf(UploadPayload::class.java, UploadPoint::class.java,
            UploadSeries::class.java, UploadSample::class.java)))
        assertTrue(reachable().containsAll(listOf(ObservationBatch::class.java, ObservationItem::class.java,
            ObservationNative::class.java)))
        assertTrue(reachable().containsAll(listOf(ScienceDiagnosticReport::class.java, ScienceMetricDiagnostic::class.java)))
    }

    @Test
    fun theFieldNamesAreTheServerContract() {
        // Gson writes the Java field names: these are the IngestWorkoutRequest properties the server expects.
        assertEquals(listOf("time", "value"), fields(UploadSample::class.java))
        assertEquals(listOf("metric", "samples"), fields(UploadSeries::class.java))
        assertEquals(listOf("time", "lat", "lon", "altitudeM", "accuracyM"), fields(UploadPoint::class.java))
        assertTrue(fields(UploadPayload::class.java).containsAll(listOf("source", "sourceId", "revision", "track", "series")))
        // IngestObservationsRequest.
        assertEquals(listOf("source", "samples"), fields(ObservationBatch::class.java))
        assertEquals(listOf("externalId", "revision", "revisionOrder", "metric", "unit", "value", "time", "method",
            "providerAlgorithm", "native"), fields(ObservationItem::class.java))
        assertEquals(listOf("metric", "unit", "value", "mappingVersion"), fields(ObservationNative::class.java))
        // ScienceDiagnosticReport: no biometric value or record identifier may enter the telemetry.
        assertEquals(listOf("source", "runId", "startedAt", "finishedAt", "state", "firstReadFrom", "readThrough",
            "windowsRead", "metrics"), fields(ScienceDiagnosticReport::class.java))
        assertEquals(listOf("metric", "permission", "read", "attempted", "confirmed", "rejected", "conflicts", "windowsRead", "readError"),
            fields(ScienceMetricDiagnostic::class.java))
    }

    private fun fields(type: Class<*>) =
        type.declaredFields.filterNot { Modifier.isStatic(it.modifiers) || it.isSynthetic }.map { it.name }

    private fun reachable(): Set<Class<*>> {
        val seen = linkedSetOf<Class<*>>()
        val pending = ArrayDeque<Type>(roots)
        while (pending.isNotEmpty()) {
            when (val type = pending.removeFirst()) {
                is ParameterizedType -> { pending += type.rawType; pending += type.actualTypeArguments }
                is Class<*> -> if (!type.isPrimitive && seen.add(type) && type.name.startsWith("it.moscorun.")) {
                    type.declaredFields.filterNot { Modifier.isStatic(it.modifiers) || it.isSynthetic }
                        .forEach { pending += it.genericType }
                }
            }
        }
        return seen
    }
}
