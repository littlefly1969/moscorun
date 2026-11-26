package it.moscorun.app.ui

import android.app.Activity
import android.content.Context
import android.graphics.Color
import android.graphics.Typeface
import android.graphics.drawable.GradientDrawable
import android.view.View
import android.widget.Button
import android.widget.LinearLayout
import android.widget.ScrollView
import android.widget.TextView
import it.moscorun.app.model.Provider
import it.moscorun.app.model.TrackPoint
import it.moscorun.app.science.ScienceAvailability
import it.moscorun.app.server.MoscoRunServer
import it.moscorun.app.settings.AppPreferences
import it.moscorun.app.settings.SecureSettings
import it.moscorun.app.source.SourceStatus
import it.moscorun.app.sync.SyncProgress
import it.moscorun.app.sync.SyncRunner
import it.moscorun.app.sync.SyncStateStore
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Job
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.cancel

internal object Palette {
    val background = Color.rgb(243, 246, 244)
    val green = Color.rgb(23, 106, 82)
    val ink = Color.rgb(24, 48, 41)
    val muted = Color.rgb(92, 110, 104)
    val warning = Color.rgb(160, 70, 40)
}

/**
 * What the screens need from the application. The activity implements it; screens never reach the sources, the
 * scheduler or the system directly, which keeps them free of anything that cannot be exercised in a plain test.
 */
interface AppHost {
    val activity: Activity
    val scope: CoroutineScope
    val settings: SecureSettings
    val prefs: AppPreferences
    val store: SyncStateStore
    val sync: SyncRunner

    /** What the run in progress is doing; null when idle. */
    val liveProgress: SyncProgress?

    /** True when the old, separate Health Connect app is still installed on the phone. */
    val oldAppInstalled: Boolean

    /** Client for the configured server, or null while the setup is incomplete. */
    fun server(): MoscoRunServer?

    suspend fun sourceStatus(provider: Provider): SourceStatus

    /** Optional permissions still missing for [provider], in words for the user. */
    suspend fun sourceHints(provider: Provider): List<String>

    /** What the scientific part can read now, in words for the user. */
    suspend fun scienceAvailability(): ScienceAvailability

    /** Shows the Health Connect consent screen for the scientific data; [done] runs when the user comes back. */
    fun requestSciencePermissions(done: () -> Unit)

    /** Shows the consent screen of the source; [done] runs when the user comes back. */
    fun requestSourcePermissions(provider: Provider, done: () -> Unit)

    /** Starts a synchronisation now, unless one is already running. */
    fun syncNow()

    /** Brings the background schedule in line with the settings. */
    fun reschedule()

    /** Forgets server, token and synchronisation state. */
    fun disconnect()

    fun open(screen: Screen)
    fun openTab(tab: Tab)
    fun back()

    /** Builds the visible screen again from its state. */
    fun refresh()

    /** Stack trace of the last crash, once. */
    fun takeCrash(): String?

    /** Shows a route on the interactive map. */
    fun openMap(title: String, points: List<TrackPoint>)

    fun openOldAppSettings()
    fun openPrivacy()
}

enum class Tab(val label: String) { HOME("Stato"), WORKOUTS("Allenamenti"), SETTINGS("Impostazioni") }

/**
 * One screen of the single activity. It keeps its own state, so its view can be rebuilt at any time; work started
 * while it is visible is cancelled when it is hidden.
 */
abstract class Screen {
    protected lateinit var host: AppHost
        private set
    protected lateinit var visible: CoroutineScope
        private set

    /** True while this screen is the one on display. */
    protected var attached = false
        private set

    fun attach(host: AppHost): View {
        this.host = host
        attached = true
        visible = CoroutineScope(host.scope.coroutineContext + SupervisorJob(host.scope.coroutineContext[Job]))
        return build()
    }

    fun detach() {
        attached = false
        if (::visible.isInitialized) visible.cancel()
    }

    protected abstract fun build(): View

    /** The app came back to the foreground with this screen on display; what the user typed must be left alone. */
    open fun resumed() {}

    protected val context: Context get() = host.activity

    /** Scrollable column with the standard margins. */
    protected fun page(fill: LinearLayout.() -> Unit): View = ScrollView(context).apply {
        isFillViewport = true
        addView(context.column().apply {
            setPadding(context.dp(18), context.dp(16), context.dp(18), context.dp(24))
            fill()
        })
    }
}

internal fun Context.dp(value: Int): Int = (value * resources.displayMetrics.density).toInt()

internal fun Context.column(): LinearLayout = LinearLayout(this).apply { orientation = LinearLayout.VERTICAL }

internal fun LinearLayout.label(value: CharSequence, size: Float = 15f, bold: Boolean = false, color: Int = Palette.ink): TextView =
    TextView(context).apply {
        text = value
        textSize = size
        setTextColor(color)
        setPadding(0, context.dp(4), 0, context.dp(4))
        if (bold) setTypeface(typeface, Typeface.BOLD)
        this@label.addView(this)
    }

internal fun LinearLayout.heading(overline: String, title: String) {
    label(overline.uppercase(), 12f, bold = true, color = Palette.green)
    label(title, 26f, bold = true)
}

internal fun LinearLayout.action(label: String, enabled: Boolean = true, onClick: () -> Unit): Button =
    Button(context).apply {
        text = label
        isAllCaps = false
        isEnabled = enabled
        setOnClickListener { onClick() }
        this@action.addView(this)
    }

internal fun LinearLayout.card(fill: LinearLayout.() -> Unit): LinearLayout = context.column().apply {
    setPadding(context.dp(14), context.dp(12), context.dp(14), context.dp(12))
    background = GradientDrawable().apply { setColor(Color.WHITE); cornerRadius = context.dp(16).toFloat() }
    this@card.addView(this, LinearLayout.LayoutParams(-1, -2).apply { topMargin = context.dp(10) })
    fill()
}
