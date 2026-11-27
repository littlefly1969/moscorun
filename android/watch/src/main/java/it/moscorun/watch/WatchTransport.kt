package it.moscorun.watch

import android.content.Context
import android.security.keystore.KeyGenParameterSpec
import android.security.keystore.KeyProperties
import android.util.AtomicFile
import it.moscorun.sensor.SensorPayload
import java.io.File
import java.net.URI
import java.security.KeyStore
import java.security.MessageDigest
import java.util.Base64
import java.util.zip.GZIPOutputStream
import javax.crypto.Cipher
import javax.crypto.KeyGenerator
import javax.crypto.SecretKey
import javax.crypto.spec.GCMParameterSpec
import javax.net.ssl.HttpsURLConnection

data class WatchConnection(val base: String, val token: String) {
    val scope: String get() = MessageDigest.getInstance("SHA-256").digest((base + "\n" + token).toByteArray())
        .joinToString("") { "%02x".format(it) }

    companion object {
        fun checked(url: String, token: String): WatchConnection {
            val uri = URI(url.trim())
            require(uri.scheme == "https" && !uri.host.isNullOrBlank() && uri.userInfo == null &&
                uri.query == null && uri.fragment == null && uri.path.orEmpty().trim('/').isEmpty()) { "Server HTTPS senza percorso obbligatorio" }
            require(token.trim().matches(Regex("mr1_[A-Za-z0-9_-]{20,200}"))) { "Token dispositivo non valido" }
            return WatchConnection(uri.toString().trimEnd('/'), token.trim())
        }
    }
}

/** Stored only in the watch's private no-backup directory. Keystore protects the token. */
class WatchVault(context: Context) {
    private val prefs = context.getSharedPreferences("watch-server", Context.MODE_PRIVATE)
    private fun key(): SecretKey {
        val store = KeyStore.getInstance("AndroidKeyStore").apply { load(null) }
        (store.getKey("watch-token", null) as? SecretKey)?.let { return it }
        return KeyGenerator.getInstance(KeyProperties.KEY_ALGORITHM_AES, "AndroidKeyStore").apply {
            init(KeyGenParameterSpec.Builder("watch-token", KeyProperties.PURPOSE_ENCRYPT or KeyProperties.PURPOSE_DECRYPT)
                .setBlockModes(KeyProperties.BLOCK_MODE_GCM).setEncryptionPaddings(KeyProperties.ENCRYPTION_PADDING_NONE)
                .setKeySize(256).build())
        }.generateKey()
    }
    fun save(connection: WatchConnection) {
        val cipher = Cipher.getInstance("AES/GCM/NoPadding").apply { init(Cipher.ENCRYPT_MODE, key()) }
        val secret = Base64.getEncoder().encodeToString(cipher.iv + cipher.doFinal(connection.token.toByteArray()))
        check(prefs.edit().putString("base", connection.base).putString("token", secret).commit())
    }
    fun load(): WatchConnection? = runCatching {
        val raw = Base64.getDecoder().decode(prefs.getString("token", null) ?: return null)
        val cipher = Cipher.getInstance("AES/GCM/NoPadding").apply {
            init(Cipher.DECRYPT_MODE, key(), GCMParameterSpec(128, raw, 0, 12))
        }
        WatchConnection.checked(prefs.getString("base", "")!!, cipher.doFinal(raw, 12, raw.size - 12).decodeToString())
    }.getOrNull()
}

class WatchQueue(context: Context) {
    private val root = File(context.noBackupFilesDir, "signal-queue")
    private fun directory(connection: WatchConnection) = File(root, connection.scope).apply { mkdirs() }

    @Synchronized fun append(connection: WatchConnection, payloads: List<SensorPayload>) {
        val dir = directory(connection)
        val bytes = dir.listFiles().orEmpty().sumOf { it.length() }
        check(bytes + payloads.sumOf { it.json.toByteArray().size.toLong() } <= 50L * 1024 * 1024) { "Archivio locale pieno" }
        for (payload in payloads) {
            val file = AtomicFile(File(dir, payload.id.replace(':', '_') + ".json"))
            val stream = file.startWrite()
            try {
                stream.write(payload.json.toByteArray())
                file.finishWrite(stream)
            } catch (e: Exception) {
                file.failWrite(stream)
                throw e
            }
        }
    }
    @Synchronized fun pending(connection: WatchConnection): List<File> =
        directory(connection).listFiles { f -> f.name.endsWith(".json") }.orEmpty().sortedBy { it.lastModified() }

    /** Retry the exact persisted payload; only acknowledged chunks are removed. No redirection of credentials. */
    fun drain(connection: WatchConnection): String {
        var acknowledged = 0
        for (file in pending(connection)) {
            val code = runCatching {
                val http = URI(connection.base + "/api/v1/ingest/signals").toURL().openConnection() as HttpsURLConnection
                try {
                    http.requestMethod = "POST"
                    http.instanceFollowRedirects = false
                    http.connectTimeout = 10_000
                    http.readTimeout = 10_000
                    http.doOutput = true
                    http.setRequestProperty("Authorization", "Bearer " + connection.token)
                    http.setRequestProperty("Content-Type", "application/json")
                    http.setRequestProperty("Content-Encoding", "gzip")
                    http.setChunkedStreamingMode(0)
                    GZIPOutputStream(http.outputStream).use { out -> AtomicFile(file).openRead().use { it.copyTo(out) } }
                    http.responseCode
                } finally { http.disconnect() }
            }.getOrElse { return "$acknowledged chunk confermati; rete non disponibile, coda conservata" }
            if (code != 200 && code != 201) return "$acknowledged chunk confermati; HTTP $code, coda conservata"
            AtomicFile(file).delete()
            acknowledged++
        }
        return "$acknowledged chunk confermati; coda vuota"
    }
}
