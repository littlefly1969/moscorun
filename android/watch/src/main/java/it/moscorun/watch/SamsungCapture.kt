package it.moscorun.watch

import android.content.Context
import com.samsung.android.service.health.tracking.ConnectionListener
import com.samsung.android.service.health.tracking.HealthTracker
import com.samsung.android.service.health.tracking.HealthTrackerException
import com.samsung.android.service.health.tracking.HealthTrackingService
import com.samsung.android.service.health.tracking.data.DataPoint
import com.samsung.android.service.health.tracking.data.HealthTrackerType
import com.samsung.android.service.health.tracking.data.ValueKey
import it.moscorun.sensor.SensorChannel
import it.moscorun.sensor.SensorChunk
import it.moscorun.sensor.SensorPayload
import it.moscorun.sensor.SensorSample
import java.util.UUID

/** The actual SDK adapter, not a simulated sensor. Each session is explicitly initiated by the user. */
class SamsungCapture(context: Context, private val status: (String) -> Unit,
    private val capabilities: (List<HealthTrackerType>) -> Unit) {
    private var tracker: HealthTracker? = null
    private var session = UUID.randomUUID()
    @Volatile private var generation = 0
    private val service: HealthTrackingService = HealthTrackingService(object : ConnectionListener {
        override fun onConnectionSuccess() {
            capabilities(service.trackingCapability.supportHealthTrackerTypes.filter { it in supported })
            status("SDK connesso: seleziona un sensore")
        }
        override fun onConnectionEnded() { status("SDK disconnesso") }
        override fun onConnectionFailed(e: HealthTrackerException) { status("Connessione SDK fallita: ${e.errorCode}") }
    }, context.applicationContext)

    fun connect() = service.connectService()

    fun start(type: HealthTrackerType, store: (List<SensorPayload>) -> Unit) {
        stop()
        require(type in supported && type in service.trackingCapability.supportHealthTrackerTypes)
        session = UUID.randomUUID()
        val run = session
        val current = ++generation
        val active = service.getHealthTracker(type)
        tracker = active
        run {
            active.setEventListener(object : HealthTracker.TrackerEventListener {
                override fun onDataReceived(list: List<DataPoint>) {
                    if (current != generation) return
                    try {
                        val result = channels(type, list).flatMap { (channel, points) ->
                            SensorChunk.encode(run, channel, points, System.currentTimeMillis())
                        }
                        if (result.isNotEmpty()) store(result)
                        status("${type.name}: ${result.sumOf { it.samples }} valori ricevuti nel lotto")
                    } catch (_: Exception) {
                        stop()
                        status("Acquisizione sospesa: timestamp, SDK o archivio locale non valido")
                    }
                }
                override fun onFlushCompleted() { status("Buffer SDK trasmesso") }
                override fun onError(error: HealthTracker.TrackerError) {
                    stop()
                    status("Acquisizione interrotta: ${error.name}")
                }
            })
        }
    }

    fun stop() {
        generation++
        tracker?.unsetEventListener()
        tracker = null
    }

    fun close() { stop(); service.disconnectService() }

    private fun channels(type: HealthTrackerType, points: List<DataPoint>): Map<SensorChannel, List<SensorSample>> {
        val output = linkedMapOf<SensorChannel, MutableList<SensorSample>>()
        fun <T : Number> add(metric: String, unit: String, field: String, key: ValueKey<T>,
            reported: Boolean = false, transform: (T) -> Double = { it.toDouble() }) {
            val channel = SensorChannel(metric, unit, type.name + ":" + field, !reported)
            for (point in points) {
                val value = runCatching { point.getValue(key) }.getOrNull() ?: continue
                output.getOrPut(channel) { mutableListOf() }.add(SensorSample(point.timestamp, transform(value)))
            }
        }
        when (type) {
            HealthTrackerType.ECG_ON_DEMAND -> {
                add("ecg", "mV", "ECG_MV", ValueKey.EcgSet.ECG_MV)
                add("ecgLeadOff", "native", "LEAD_OFF", ValueKey.EcgSet.LEAD_OFF)
                add("ecgSequence", "native", "SEQUENCE", ValueKey.EcgSet.SEQUENCE)
                add("ecgMaxThreshold", "mV", "MAX_THRESHOLD_MV", ValueKey.EcgSet.MAX_THRESHOLD_MV)
                add("ecgMinThreshold", "mV", "MIN_THRESHOLD_MV", ValueKey.EcgSet.MIN_THRESHOLD_MV)
                add("ppgGreen", "native", "PPG_GREEN", ValueKey.EcgSet.PPG_GREEN)
            }
            HealthTrackerType.PPG_CONTINUOUS, HealthTrackerType.PPG_ON_DEMAND -> {
                add("ppgGreen", "native", "PPG_GREEN", ValueKey.PpgSet.PPG_GREEN)
                add("ppgInfrared", "native", "PPG_IR", ValueKey.PpgSet.PPG_IR)
                add("ppgRed", "native", "PPG_RED", ValueKey.PpgSet.PPG_RED)
                add("ppgGreenStatus", "native", "GREEN_STATUS", ValueKey.PpgSet.GREEN_STATUS)
                add("ppgInfraredStatus", "native", "IR_STATUS", ValueKey.PpgSet.IR_STATUS)
                add("ppgRedStatus", "native", "RED_STATUS", ValueKey.PpgSet.RED_STATUS)
            }
            HealthTrackerType.ACCELEROMETER_CONTINUOUS -> {
                add("accelerationRawX", "native", "ACCELEROMETER_X", ValueKey.AccelerometerSet.ACCELEROMETER_X)
                add("accelerationX", "m/s2", "ACCELEROMETER_X", ValueKey.AccelerometerSet.ACCELEROMETER_X) { SensorChunk.acceleration(it) }
                add("accelerationRawY", "native", "ACCELEROMETER_Y", ValueKey.AccelerometerSet.ACCELEROMETER_Y)
                add("accelerationY", "m/s2", "ACCELEROMETER_Y", ValueKey.AccelerometerSet.ACCELEROMETER_Y) { SensorChunk.acceleration(it) }
                add("accelerationRawZ", "native", "ACCELEROMETER_Z", ValueKey.AccelerometerSet.ACCELEROMETER_Z)
                add("accelerationZ", "m/s2", "ACCELEROMETER_Z", ValueKey.AccelerometerSet.ACCELEROMETER_Z) { SensorChunk.acceleration(it) }
            }
            HealthTrackerType.HEART_RATE_CONTINUOUS -> {
                add("heartRate", "bpm", "HEART_RATE", ValueKey.HeartRateSet.HEART_RATE, reported = true)
                add("heartRateStatus", "native", "HEART_RATE_STATUS", ValueKey.HeartRateSet.HEART_RATE_STATUS)
                // The SDK gives an ordered batch, not a timestamp for every beat. Keep its event time and slot.
                for (point in points) {
                    val intervals = runCatching { point.getValue(ValueKey.HeartRateSet.IBI_LIST) }.getOrNull().orEmpty()
                    val flags = runCatching { point.getValue(ValueKey.HeartRateSet.IBI_STATUS_LIST) }.getOrNull().orEmpty()
                    intervals.forEachIndexed { index, value ->
                        val channel = SensorChannel("pulseInterval", "ms", type.name + ":IBI_LIST[$index]", false)
                        output.getOrPut(channel) { mutableListOf() }.add(SensorSample(point.timestamp, value.toDouble()))
                    }
                    flags.forEachIndexed { index, value ->
                        val channel = SensorChannel("pulseIntervalStatus", "native", type.name + ":IBI_STATUS_LIST[$index]", false)
                        output.getOrPut(channel) { mutableListOf() }.add(SensorSample(point.timestamp, value.toDouble()))
                    }
                }
            }
            HealthTrackerType.EDA_CONTINUOUS -> {
                add("eda", "uS", "SKIN_CONDUCTANCE", ValueKey.EdaSet.SKIN_CONDUCTANCE)
                add("edaStatus", "native", "STATUS", ValueKey.EdaSet.STATUS)
            }
            HealthTrackerType.SKIN_TEMPERATURE_CONTINUOUS, HealthTrackerType.SKIN_TEMPERATURE_ON_DEMAND -> {
                add("skinTemperature", "Cel", "OBJECT_TEMPERATURE", ValueKey.SkinTemperatureSet.OBJECT_TEMPERATURE)
                add("watchAmbientTemperature", "Cel", "AMBIENT_TEMPERATURE", ValueKey.SkinTemperatureSet.AMBIENT_TEMPERATURE)
                add("skinTemperatureStatus", "native", "STATUS", ValueKey.SkinTemperatureSet.STATUS)
            }
            HealthTrackerType.SPO2_ON_DEMAND -> {
                add("spo2", "%", "SPO2", ValueKey.SpO2Set.SPO2, reported = true)
                add("heartRate", "bpm", "HEART_RATE", ValueKey.SpO2Set.HEART_RATE, reported = true)
                add("spo2Status", "native", "STATUS", ValueKey.SpO2Set.STATUS)
            }
            HealthTrackerType.MF_BIA_ON_DEMAND -> {
                add("mfBiaStatus", "native", "STATUS", ValueKey.MfBiaSet.STATUS)
                add("mfBiaProgress", "native", "PROGRESS", ValueKey.MfBiaSet.PROGRESS)
                add("mfBiaMagnitude5k", "native", "BODY_IMPEDANCE_MAGNITUDE_5K", ValueKey.MfBiaSet.BODY_IMPEDANCE_MAGNITUDE_5K)
                add("mfBiaPhase5k", "native", "BODY_IMPEDANCE_PHASE_5K", ValueKey.MfBiaSet.BODY_IMPEDANCE_PHASE_5K)
                add("mfBiaMagnitude10k", "native", "BODY_IMPEDANCE_MAGNITUDE_10K", ValueKey.MfBiaSet.BODY_IMPEDANCE_MAGNITUDE_10K)
                add("mfBiaPhase10k", "native", "BODY_IMPEDANCE_PHASE_10K", ValueKey.MfBiaSet.BODY_IMPEDANCE_PHASE_10K)
                add("mfBiaMagnitude50k", "native", "BODY_IMPEDANCE_MAGNITUDE_50K", ValueKey.MfBiaSet.BODY_IMPEDANCE_MAGNITUDE_50K)
                add("mfBiaPhase50k", "native", "BODY_IMPEDANCE_PHASE_50K", ValueKey.MfBiaSet.BODY_IMPEDANCE_PHASE_50K)
                add("mfBiaMagnitude250k", "native", "BODY_IMPEDANCE_MAGNITUDE_250K", ValueKey.MfBiaSet.BODY_IMPEDANCE_MAGNITUDE_250K)
                add("mfBiaPhase250k", "native", "BODY_IMPEDANCE_PHASE_250K", ValueKey.MfBiaSet.BODY_IMPEDANCE_PHASE_250K)
            }
            else -> error("Tracker non ancora integrato")
        }
        return output
    }

    companion object {
        val supported = setOf(HealthTrackerType.ECG_ON_DEMAND, HealthTrackerType.PPG_CONTINUOUS,
            HealthTrackerType.PPG_ON_DEMAND, HealthTrackerType.ACCELEROMETER_CONTINUOUS,
            HealthTrackerType.HEART_RATE_CONTINUOUS, HealthTrackerType.EDA_CONTINUOUS,
            HealthTrackerType.SKIN_TEMPERATURE_CONTINUOUS, HealthTrackerType.SKIN_TEMPERATURE_ON_DEMAND,
            HealthTrackerType.SPO2_ON_DEMAND, HealthTrackerType.MF_BIA_ON_DEMAND)
    }
}
