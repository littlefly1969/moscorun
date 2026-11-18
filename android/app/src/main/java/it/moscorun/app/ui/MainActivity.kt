package it.moscorun.app.ui

import android.app.Activity
import android.content.Intent
import android.graphics.Typeface
import android.net.Uri
import android.os.Bundle
import android.provider.Settings
import android.view.Gravity
import android.view.WindowManager
import android.widget.FrameLayout
import android.widget.LinearLayout
import android.widget.TextView
import androidx.activity.ComponentActivity
import androidx.activity.OnBackPressedCallback
import androidx.health.connect.client.PermissionController
import it.moscorun.app.AppGraph
import it.moscorun.app.CrashLog
import it.moscorun.app.model.Provider
import it.moscorun.app.model.TrackPoint
import it.moscorun.app.settings.ServerSetup
import it.moscorun.app.source.SourceStatus
import it.moscorun.app.sync.SyncScheduler
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.MainScope
import kotlinx.coroutines.cancel
import kotlinx.coroutines.launch

/**
 * The only interface of the app: three tabs and a small stack of screens. It shows what the server holds and the
 * state of the synchronisation; the synchronisation itself runs in the background worker, also while this is open.
 */
class MainActivity : ComponentActivity(), AppHost {
    private val graph by lazy { AppGraph.of(this) }
    private lateinit var content: FrameLayout
    private lateinit var tabs: LinearLayout
    private val roots by lazy {
        mapOf(Tab.HOME to HomeScreen(), Tab.WORKOUTS to WorkoutsScreen(),
            Tab.SETTINGS to SettingsScreen { ServerSetup(graph.settings, graph.prefs, graph.sync, graph::api) })
    }
    private var tab = Tab.HOME
    private val stack = ArrayList<Screen>()
    private var afterPermissions: (() -> Unit)? = null
    private val healthConnectPermissions = registerForActivityResult(PermissionController.createRequestPermissionResultContract()) {
        afterPermissions?.also { afterPermissions = null }?.invoke()
    }
    private val backToPrevious = object : OnBackPressedCallback(false) {
        override fun handleOnBackPressed() = back()
    }

    override val activity: Activity get() = this
    override val scope: CoroutineScope = MainScope()
    override val settings get() = graph.settings
    override val prefs get() = graph.prefs
    override val store get() = graph.store
    override val sync get() = graph.sync
    override val liveProgress get() = graph.liveProgress
    override val oldAppInstalled: Boolean
        get() = runCatching { packageManager.getPackageInfo(OLD_HEALTH_CONNECT_APP, 0) }.isSuccess

    override fun onCreate(state: Bundle?) {
        super.onCreate(state)
        // Health data on screen: keep it out of screenshots and of the recent-apps preview.
        window.addFlags(WindowManager.LayoutParams.FLAG_SECURE)
        content = FrameLayout(this)
        tabs = LinearLayout(this).apply { orientation = LinearLayout.HORIZONTAL }
        val root = column().apply {
            setBackgroundColor(Palette.background)
            addView(content, LinearLayout.LayoutParams(-1, 0, 1f))
            addView(tabs, LinearLayout.LayoutParams(-1, -2))
        }
        setContentView(root)
        // Edge-to-edge is mandatory from Android 15: leave room for system bars and display cutouts.
        root.setOnApplyWindowInsetsListener { view, insets ->
            @Suppress("DEPRECATION")
            view.setPadding(insets.systemWindowInsetLeft, insets.systemWindowInsetTop, insets.systemWindowInsetRight, insets.systemWindowInsetBottom)
            insets
        }
        root.requestApplyInsets()
        onBackPressedDispatcher.addCallback(this, backToPrevious)
        tab = Tab.entries.getOrElse(state?.getInt(STATE_TAB) ?: 0) { Tab.HOME }
        openTab(tab)
    }

    override fun onSaveInstanceState(outState: Bundle) {
        super.onSaveInstanceState(outState)
        outState.putInt(STATE_TAB, tab.ordinal)
    }

    override fun onResume() {
        super.onResume()
        // Permissions may have been changed in the system settings while the app was in the background.
        stack.lastOrNull()?.resumed()
    }

    override fun onDestroy() {
        stack.forEach(Screen::detach)
        scope.cancel()
        super.onDestroy()
    }

    override fun server() = graph.server()

    override suspend fun sourceStatus(provider: Provider): SourceStatus = graph.source(provider).status()

    override suspend fun sourceHints(provider: Provider): List<String> =
        if (provider == Provider.HEALTH_CONNECT) graph.healthConnect.hints() else emptyList()

    override fun requestSourcePermissions(provider: Provider, done: () -> Unit) {
        when (provider) {
            Provider.SAMSUNG_HEALTH -> scope.launch {
                graph.samsung.requestPermissions(this@MainActivity)
                done()
            }
            Provider.HEALTH_CONNECT -> scope.launch {
                val requestable = graph.healthConnect.requestable()
                if (requestable.isEmpty()) { done(); return@launch }
                afterPermissions = done
                healthConnectPermissions.launch(requestable)
            }
        }
    }

    override fun syncNow() {
        // A run of the user replaces one that is only waiting; one that is working is left to finish.
        if (!graph.sync.running) SyncScheduler.runSoon(this, replace = true)
    }

    override fun reschedule() = graph.reschedule()

    override fun disconnect() = graph.disconnect()

    override fun open(screen: Screen) {
        stack.lastOrNull()?.detach()
        stack += screen
        show()
    }

    override fun openTab(tab: Tab) {
        stack.forEach(Screen::detach)
        stack.clear()
        this.tab = tab
        stack += roots.getValue(tab)
        show()
    }

    override fun back() {
        if (stack.size <= 1) return
        stack.removeAt(stack.lastIndex).detach()
        show()
    }

    override fun refresh() {
        stack.lastOrNull()?.detach()
        show()
    }

    private fun show() {
        if (isDestroyed) return
        backToPrevious.isEnabled = stack.size > 1
        content.removeAllViews()
        content.addView(stack.last().attach(this))
        tabs.removeAllViews()
        Tab.entries.forEach { entry ->
            tabs.addView(TextView(this).apply {
                text = entry.label
                gravity = Gravity.CENTER
                textSize = 14f
                setPadding(0, dp(14), 0, dp(14))
                setTextColor(if (entry == tab) Palette.green else Palette.muted)
                if (entry == tab) setTypeface(typeface, Typeface.BOLD)
                isClickable = true
                isFocusable = true
                contentDescription = "Scheda ${entry.label}"
                setOnClickListener { openTab(entry) }
            }, LinearLayout.LayoutParams(0, -2, 1f))
        }
    }

    override fun takeCrash(): String? = CrashLog.take(this)

    override fun openMap(title: String, points: List<TrackPoint>) = openGoogleRouteMap(this, title, points)

    override fun openOldAppSettings() {
        runCatching { startActivity(Intent(Settings.ACTION_APPLICATION_DETAILS_SETTINGS, Uri.fromParts("package", OLD_HEALTH_CONNECT_APP, null))) }
    }

    override fun openPrivacy() = startActivity(Intent(this, PrivacyActivity::class.java))

    private companion object {
        const val STATE_TAB = "tab"

        /** Application id of the separate Health Connect app of the previous versions, now part of this one. */
        const val OLD_HEALTH_CONNECT_APP = "it.littlefly.moscorun.hc"
    }
}
