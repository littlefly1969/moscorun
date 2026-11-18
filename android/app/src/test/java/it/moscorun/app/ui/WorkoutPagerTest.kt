package it.moscorun.app.ui

import it.moscorun.app.server.Failure
import it.moscorun.app.server.ReadResult
import it.moscorun.app.server.RemoteWorkout
import it.moscorun.app.server.WorkoutPage
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

class WorkoutPagerTest {
    private fun workout(n: Int) = RemoteWorkout("00000000-0000-0000-0000-%012d".format(n), "samsung-health", "s$n", "RUNNING",
        "2026-11-01T08:00:00Z", "2026-11-01T09:00:00Z", null, null, null, null, null, 0)

    private val asked = mutableListOf<String?>()
    private var answers = ArrayDeque<ReadResult<WorkoutPage>>()
    private val pager = WorkoutPager { cursor -> asked += cursor; answers.removeFirst() }

    private fun page(range: IntRange, next: String?) = ReadResult.Ok(WorkoutPage(range.map(::workout), next))

    @Test fun `pages are appended in server order following the cursor`() {
        answers += page(1..3, "c1")
        answers += page(4..5, null)

        assertTrue(pager.hasMore)
        assertTrue(pager.loadNext())
        assertTrue(pager.hasMore)
        assertTrue(pager.loadNext())

        assertEquals(listOf("s1", "s2", "s3", "s4", "s5"), pager.items.map { it.sourceId })
        assertEquals(listOf(null, "c1"), asked)
        assertFalse(pager.hasMore)
    }

    @Test fun `nothing is asked after the last page`() {
        answers += page(1..2, null)
        pager.loadNext()

        assertTrue(pager.loadNext())

        assertEquals(1, asked.size)
    }

    @Test fun `an empty server is a loaded, empty list`() {
        answers += page(IntRange.EMPTY, null)

        assertTrue(pager.loadNext())

        assertTrue(pager.started)
        assertTrue(pager.items.isEmpty())
        assertFalse(pager.hasMore)
    }

    @Test fun `a failure keeps what was loaded and retries the same page`() {
        answers += page(1..2, "c1")
        answers += ReadResult.Failed("offline", Failure.OFFLINE)
        answers += page(3..4, null)
        pager.loadNext()

        assertFalse(pager.loadNext())
        assertNotNull(pager.failure)
        assertEquals(2, pager.items.size)
        assertTrue(pager.hasMore)

        assertTrue(pager.loadNext())
        assertNull(pager.failure)
        assertEquals(listOf(null, "c1", "c1"), asked)
        assertEquals(4, pager.items.size)
    }

    @Test fun `a first page that fails can be retried`() {
        answers += ReadResult.Failed("403", Failure.FORBIDDEN)
        answers += page(1..1, null)

        assertFalse(pager.loadNext())
        assertFalse(pager.started)
        assertTrue(pager.loadNext())
        assertEquals(1, pager.items.size)
    }

    @Test fun `a workout repeated across pages is shown once`() {
        answers += page(1..3, "c1")
        answers += page(3..4, null)
        pager.loadNext()
        pager.loadNext()

        assertEquals(listOf("s1", "s2", "s3", "s4"), pager.items.map { it.sourceId })
    }

    @Test fun `a cursor that does not move ends the list instead of looping`() {
        answers += page(1..2, "c1")
        answers += page(3..4, "c1")
        pager.loadNext()
        pager.loadNext()

        assertFalse(pager.hasMore)
    }

    @Test fun `every kind of failure has words for the user`() {
        Failure.entries.forEach { kind ->
            assertTrue(WorkoutsScreen.describe(ReadResult.Failed("dettaglio", kind)).isNotBlank())
        }
    }
}
