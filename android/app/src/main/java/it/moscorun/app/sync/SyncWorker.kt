package it.moscorun.app.sync

import android.content.Context
import androidx.work.CoroutineWorker
import androidx.work.WorkerParameters
import it.moscorun.app.AppGraph
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext

/**
 * Runs one synchronisation when the system decides it is a good moment. It never shows an interface and never
 * asks for consent: when the source or the token need the user, the run records why and ends, and the app shows
 * it at the next opening.
 */
class SyncWorker(context: Context, params: WorkerParameters) : CoroutineWorker(context, params) {
    override suspend fun doWork(): Result {
        val graph = AppGraph.of(applicationContext)
        val outcome = try {
            withContext(Dispatchers.IO) { graph.sync.run(shouldStop = { isStopped }, progress = { graph.liveProgress = it }) }
        } catch (e: CancellationException) {
            throw e
        } catch (_: Exception) {
            // An unexpected failure must not disable the schedule: a few retries, then the next period tries again.
            return if (runAttemptCount + 1 < MAX_ATTEMPTS) Result.retry() else Result.failure()
        } finally {
            graph.liveProgress = null
        }
        return when (WorkerDecision.after(outcome, runAttemptCount, MAX_ATTEMPTS)) {
            WorkerDecision.DONE -> Result.success()
            WorkerDecision.CONTINUE -> {
                // A long first synchronisation proceeds one bounded run after another instead of one endless task.
                SyncScheduler.runSoon(applicationContext, graph.prefs.unmeteredOnly, delaySeconds = CONTINUE_DELAY_SECONDS, continuation = true)
                Result.success()
            }
            WorkerDecision.RETRY -> Result.retry()
        }
    }

    private companion object {
        const val MAX_ATTEMPTS = 5
        const val CONTINUE_DELAY_SECONDS = 30L
    }
}
