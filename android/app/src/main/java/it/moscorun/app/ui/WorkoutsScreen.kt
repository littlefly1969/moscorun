package it.moscorun.app.ui

import android.view.View
import android.view.ViewGroup
import android.widget.AbsListView
import android.widget.BaseAdapter
import android.widget.LinearLayout
import android.widget.ListView
import android.widget.TextView
import it.moscorun.app.server.Failure
import it.moscorun.app.server.ReadResult
import it.moscorun.app.server.RemoteWorkout
import it.moscorun.app.server.WorkoutFormat
import it.moscorun.app.server.WorkoutPage
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext

/**
 * Pages of the server list as they are loaded. Kept apart from the view: the order is the server's, a page is
 * never asked twice and an entry already present is not shown twice when the list changes between two requests.
 */
class WorkoutPager(private val fetch: (cursor: String?) -> ReadResult<WorkoutPage>) {
    private val seen = HashSet<String>()
    private val loaded = ArrayList<RemoteWorkout>()

    val items: List<RemoteWorkout> get() = loaded
    var failure: ReadResult.Failed? = null
        private set
    var started = false
        private set
    private var cursor: String? = null

    /** True while the server says there is more to read. */
    val hasMore: Boolean get() = !started || cursor != null

    /** Asks the server for the next page without touching the list: safe on a background thread. */
    fun fetchNext(): ReadResult<WorkoutPage> = fetch(cursor)

    /** Adds a fetched page to the list; call it on the thread that shows the list. Returns false when it failed. */
    fun accept(page: ReadResult<WorkoutPage>): Boolean = when (page) {
        is ReadResult.Failed -> { failure = page; false }
        is ReadResult.Ok -> {
            failure = null
            started = true
            page.value.items.filterTo(loaded) { seen.add(it.id) }
            // A cursor that does not move would ask the same page forever.
            cursor = page.value.nextCursor?.takeIf { it != cursor }
            true
        }
    }

    fun loadNext(): Boolean = !hasMore || accept(fetchNext())
}

/** The workouts held by the server, newest first, loaded one page at a time while scrolling. */
class WorkoutsScreen : Screen() {
    private var pager: WorkoutPager? = null
    private var loading = false
    private var position = 0
    private var footer: TextView? = null
    private val adapter = Rows()

    override fun build(): View {
        val server = host.server()
        if (server == null) return page {
            heading("Dal tuo server", "Allenamenti")
            label("Per vedere gli allenamenti configura prima il server.", 14f)
            action("Vai alle impostazioni") { host.openTab(Tab.SETTINGS) }
        }
        val pager = pager ?: WorkoutPager { cursor -> server.workouts(PAGE_SIZE, cursor) }.also { pager = it }
        val list = ListView(context).apply {
            divider = null
            footer = TextView(context).apply {
                setPadding(context.dp(18), context.dp(14), context.dp(18), context.dp(24))
                textSize = 14f
                setTextColor(Palette.muted)
                setOnClickListener { if (pager.failure != null) loadMore() }
            }
            addFooterView(footer, null, false)
            adapter = this@WorkoutsScreen.adapter
            setSelection(position)
            setOnItemClickListener { _, _, index, _ ->
                position = firstVisiblePosition
                pager.items.getOrNull(index)?.let { host.open(DetailScreen(it)) }
            }
            setOnScrollListener(object : AbsListView.OnScrollListener {
                override fun onScrollStateChanged(view: AbsListView?, state: Int) {}
                override fun onScroll(view: AbsListView?, first: Int, visibleCount: Int, total: Int) {
                    // An error stops the automatic loading: it resumes when the user taps the footer.
                    if (pager.failure == null && pager.started && first + visibleCount >= total - PREFETCH) loadMore()
                }
            })
        }
        val root = context.column().apply {
            addView(context.column().apply {
                setPadding(context.dp(18), context.dp(16), context.dp(18), context.dp(4))
                heading("Dal tuo server", "Allenamenti")
                action("Aggiorna") { reload() }
            })
            addView(list, LinearLayout.LayoutParams(-1, 0, 1f))
        }
        showFooter()
        if (!pager.started && pager.failure == null) loadMore()
        return root
    }

    private fun reload() {
        if (loading) return
        pager = null
        position = 0
        adapter.notifyDataSetChanged()
        host.refresh()
    }

    private fun loadMore() {
        val pager = pager ?: return
        if (loading || !pager.hasMore) return
        loading = true
        showFooter()
        // The request belongs to the activity, not to the visible view: leaving the screen must not lose the page.
        host.scope.launch {
            try { pager.accept(withContext(Dispatchers.IO) { pager.fetchNext() }) } finally { loading = false }
            if (pager === this@WorkoutsScreen.pager) { adapter.notifyDataSetChanged(); showFooter() }
        }
    }

    private fun showFooter() {
        val pager = pager ?: return
        val failure = pager.failure
        footer?.text = when {
            loading -> "Caricamento…"
            failure != null -> describe(failure) + "\nTocca per riprovare."
            pager.started && pager.items.isEmpty() -> "Sul server non ci sono ancora allenamenti. Avvia la sincronizzazione dalla schermata Stato."
            pager.hasMore -> ""
            else -> "${pager.items.size} allenamenti · fine dell'elenco"
        }
    }

    private inner class Rows : BaseAdapter() {
        override fun getCount() = pager?.items?.size ?: 0
        override fun getItem(index: Int) = pager?.items?.get(index)
        override fun getItemId(index: Int) = index.toLong()
        override fun getView(index: Int, convert: View?, parent: ViewGroup?): View {
            val row = (convert as? LinearLayout) ?: context.column().apply {
                setPadding(context.dp(18), context.dp(10), context.dp(18), context.dp(10))
                label("", 16f, bold = true)
                label("", 13f, color = Palette.muted)
            }
            val workout = pager?.items?.getOrNull(index) ?: return row
            (row.getChildAt(0) as TextView).text = "${WorkoutFormat.activity(workout.activityType)} · ${WorkoutFormat.distance(workout.distanceM)} · ${WorkoutFormat.duration(workout.seconds)}"
            (row.getChildAt(1) as TextView).text = listOfNotNull(
                WorkoutFormat.dateTime(workout.startedAt),
                WorkoutFormat.pace(workout.seconds, workout.distanceM).takeIf { it != "—" },
                workout.averageHeartRate?.let { WorkoutFormat.bpm(it) },
                "GPS".takeIf { workout.trackPointCount > 0 }).joinToString(" · ")
            return row
        }
    }

    companion object {
        const val PAGE_SIZE = 50
        private const val PREFETCH = 10

        /** What a failed read means for the user, case by case. */
        internal fun describe(failure: ReadResult.Failed): String = when (failure.kind) {
            Failure.OFFLINE -> "Sei offline o il server non è raggiungibile."
            Failure.UNAUTHORIZED -> "Il server non accetta il token: sostituiscilo nelle Impostazioni."
            Failure.FORBIDDEN -> "Questo token non può leggere gli allenamenti: registra il telefono come \"Telefono Android\" e sostituisci il token."
            Failure.NOT_FOUND -> "Non disponibile sul server."
            Failure.UNAVAILABLE -> "Il server non è disponibile in questo momento."
            Failure.UNEXPECTED -> failure.message
        }
    }
}
