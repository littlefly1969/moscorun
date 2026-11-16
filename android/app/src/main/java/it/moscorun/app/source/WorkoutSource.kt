package it.moscorun.app.source

import it.moscorun.app.model.Provider
import it.moscorun.app.model.SourceWorkout
import java.time.Instant

/** Whether a source can be read right now, without showing anything to the user. */
sealed interface SourceStatus {
    val message: String

    data object Ready : SourceStatus {
        override val message = "Pronta."
    }

    /** The user must grant something from the app. */
    data class NeedsPermission(override val message: String) : SourceStatus

    /** Not usable on this phone as it is: [message] says why and what to do. */
    data class Unavailable(override val message: String) : SourceStatus
}

/** A page of complete workouts in source order; [nextToken] is null on the last page. */
data class SourcePage(val workouts: List<SourceWorkout>, val nextToken: String?)

/**
 * Reading stopped for a reason only the user can remove (revoked permission, consent needed in the foreground,
 * source app to update). Never used for transient failures.
 */
class SourceBlockedException(message: String) : Exception(message)

/**
 * A place workouts are read from. Implementations only read: they never write to or delete from the source,
 * and never start an activity, so they can run in the background.
 */
interface WorkoutSource {
    val provider: Provider

    suspend fun status(): SourceStatus

    /**
     * Workouts starting in [start, end], oldest first, at most [pageSize] source records per call.
     * Pass [pageToken] from the previous page to continue; tokens are valid only within the same run.
     */
    suspend fun read(start: Instant, end: Instant, pageToken: String?, pageSize: Int): SourcePage
}
