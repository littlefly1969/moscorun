package it.moscorun.probe

/** Remembers what each workout looked like when the server last accepted it. Contains no health data. */
interface SyncLedger {
    fun isSent(sourceId: String, fingerprint: String): Boolean
    fun markSent(sourceId: String, fingerprint: String)
    fun clear()
}

data class SyncReport(val created: Int, val stored: Int, val skipped: Int, val notUploadable: Int,
    val failures: List<String>, val stoppedEarly: Boolean) {
    fun describe(): String = buildString {
        append("Server: $created nuovi, $stored aggiornati o già presenti, $skipped invariati dall'ultimo invio")
        if (notUploadable > 0) append(", $notUploadable non inviabili")
        append('.')
        if (stoppedEarly) append(" Invio interrotto.")
        failures.take(5).forEach { append("\n• ").append(it) }
        if (failures.size > 5) append("\n• … altri ${failures.size - 5} errori")
    }
}

/** Uploads workouts one by one; an authentication error stops the run because every next request would fail too. */
class WorkoutSync(private val send: (String) -> UploadResult, private val ledger: SyncLedger) {
    fun run(workouts: List<Workout>, progress: (Int, Int) -> Unit = { _, _ -> }): SyncReport {
        var created = 0; var stored = 0; var skipped = 0; var notUploadable = 0
        val failures = mutableListOf<String>()
        workouts.forEachIndexed { index, w ->
            progress(index + 1, workouts.size)
            val payload = UploadMapper.toPayload(w)
            if (payload == null) { notUploadable++; return@forEachIndexed }
            val json = UploadMapper.json(payload)
            val fingerprint = UploadMapper.fingerprint(json)
            if (ledger.isSent(payload.sourceId, fingerprint)) { skipped++; return@forEachIndexed }
            val result = send(json)
            when (result.outcome) {
                UploadOutcome.CREATED -> { created++; ledger.markSent(payload.sourceId, fingerprint) }
                UploadOutcome.STORED -> { stored++; ledger.markSent(payload.sourceId, fingerprint) }
                UploadOutcome.UNAUTHORIZED -> {
                    failures += result.detail ?: "Non autorizzato"
                    return SyncReport(created, stored, skipped, notUploadable, failures, stoppedEarly = true)
                }
                else -> failures += "${w.title} ${w.start}: ${result.outcome.name.lowercase()} ${result.detail.orEmpty()}".trim()
            }
        }
        return SyncReport(created, stored, skipped, notUploadable, failures, stoppedEarly = false)
    }
}
