package it.moscorun.app.sync

import it.moscorun.app.server.RemoteWorkout
import java.time.Duration
import java.time.Instant

/**
 * Recognises a workout the server already holds under another identity. It happens after switching between
 * Samsung Health and Health Connect, which expose the same session under unrelated identifiers, and when a
 * source hands out new identifiers for old records (a restored Samsung Health, two apps writing the same
 * session to Health Connect).
 *
 * The only criterion is physical: one person cannot do two workouts at the same time, so a session that
 * overlaps most of another one is the same session seen under another identity. Distance, type and
 * calories are deliberately ignored, because the sources round and classify them differently.
 *
 * A match is never merged or deleted: the workout is held back and shown to the user, who can send it anyway.
 */
object DuplicateGuard {
    /** Share of the shorter session that must be in common. */
    const val MIN_OVERLAP = 0.6

    /** How far around a block the server is asked for workouts, so sessions straddling its edges are seen. */
    val MARGIN: Duration = Duration.ofDays(1)

    fun twin(start: Instant, end: Instant, ownSource: String, ownSourceId: String, remote: List<RemoteWorkout>): RemoteWorkout? =
        remote.firstOrNull { !(it.source == ownSource && it.sourceId == ownSourceId) && overlap(start, end, it) >= MIN_OVERLAP }

    internal fun overlap(start: Instant, end: Instant, other: RemoteWorkout): Double {
        val otherStart = Instant.parse(other.startedAt)
        val otherEnd = Instant.parse(other.endedAt)
        val common = Duration.between(maxOf(start, otherStart), minOf(end, otherEnd)).toMillis()
        val shorter = minOf(Duration.between(start, end).toMillis(), Duration.between(otherStart, otherEnd).toMillis())
        return if (common <= 0 || shorter <= 0) 0.0 else common.toDouble() / shorter
    }
}
