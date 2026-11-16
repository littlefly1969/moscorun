package it.moscorun.probe

import android.content.Context
import java.io.File
import java.io.PrintWriter
import java.io.StringWriter
import java.time.Instant

/**
 * Keeps the stack trace of the last crash in the app's private storage, so that it can be shown and copied at the next
 * start without adb or a crash-reporting service. Only the error is written, never health data.
 */
object CrashLog {
    private const val FILE = "ultimo-arresto.txt"
    @Volatile private var installed = false

    fun install(context: Context) {
        if (installed) return
        installed = true
        val file = File(context.filesDir, FILE)
        val version = runCatching { context.packageManager.getPackageInfo(context.packageName, 0).versionName }.getOrNull() ?: "?"
        val previous = Thread.getDefaultUncaughtExceptionHandler()
        Thread.setDefaultUncaughtExceptionHandler { thread, error ->
            runCatching { file.writeText(describe(error, thread.name, version)) }
            previous?.uncaughtException(thread, error)
        }
    }

    /** Returns the last crash report once, then forgets it. */
    fun take(context: Context): String? {
        val file = File(context.filesDir, FILE)
        if (!file.exists()) return null
        return runCatching { file.readText() }.getOrNull().also { file.delete() }
    }

    internal fun describe(error: Throwable, thread: String, version: String, at: Instant = Instant.now()): String {
        val trace = StringWriter().also { error.printStackTrace(PrintWriter(it)) }.toString()
        val runtime = Runtime.getRuntime()
        return "MoscoRun $version · ${android.os.Build.MANUFACTURER} ${android.os.Build.MODEL} · Android ${android.os.Build.VERSION.RELEASE}\n" +
            "$at · thread $thread · memoria max ${runtime.maxMemory() / 1_048_576} MB\n\n" + trace.take(20_000)
    }
}
