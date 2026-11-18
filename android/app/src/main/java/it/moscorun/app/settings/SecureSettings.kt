package it.moscorun.app.settings

import android.content.Context
import android.content.SharedPreferences
import android.security.keystore.KeyGenParameterSpec
import android.security.keystore.KeyProperties
import it.moscorun.app.server.MoscoRunServer
import java.security.KeyStore
import java.util.Base64
import javax.crypto.Cipher
import javax.crypto.KeyGenerator
import javax.crypto.SecretKey
import javax.crypto.spec.GCMParameterSpec

/** Seals the device token. The production implementation keeps its key in the Android Keystore. */
interface TokenCipher {
    fun seal(plain: ByteArray): ByteArray
    fun open(sealed: ByteArray): ByteArray
    fun forget()
}

/** AES-256-GCM with a key that never leaves the Android Keystore. Stored form: 12-byte IV followed by the ciphertext. */
class KeystoreTokenCipher(private val alias: String = "moscorun-device-token") : TokenCipher {
    override fun seal(plain: ByteArray): ByteArray {
        val cipher = Cipher.getInstance(TRANSFORMATION).apply { init(Cipher.ENCRYPT_MODE, key()) }
        return cipher.iv + cipher.doFinal(plain)
    }

    override fun open(sealed: ByteArray): ByteArray {
        val cipher = Cipher.getInstance(TRANSFORMATION)
        cipher.init(Cipher.DECRYPT_MODE, key(), GCMParameterSpec(128, sealed, 0, IV_BYTES))
        return cipher.doFinal(sealed, IV_BYTES, sealed.size - IV_BYTES)
    }

    override fun forget() {
        KeyStore.getInstance(KEYSTORE).apply { load(null) }.deleteEntry(alias)
    }

    private fun key(): SecretKey {
        val store = KeyStore.getInstance(KEYSTORE).apply { load(null) }
        (store.getKey(alias, null) as? SecretKey)?.let { return it }
        return KeyGenerator.getInstance(KeyProperties.KEY_ALGORITHM_AES, KEYSTORE).apply {
            init(KeyGenParameterSpec.Builder(alias, KeyProperties.PURPOSE_ENCRYPT or KeyProperties.PURPOSE_DECRYPT)
                .setBlockModes(KeyProperties.BLOCK_MODE_GCM)
                .setEncryptionPaddings(KeyProperties.ENCRYPTION_PADDING_NONE)
                .setKeySize(256).build())
        }.generateKey()
    }

    private companion object {
        const val KEYSTORE = "AndroidKeyStore"
        const val TRANSFORMATION = "AES/GCM/NoPadding"
        const val IV_BYTES = 12
    }
}

sealed interface TokenState {
    /** No server has been configured. */
    data object Missing : TokenState

    class Available(val token: String) : TokenState

    /**
     * A token is stored but can no longer be decrypted (Keystore key lost, e.g. after a device restore):
     * the user is asked to enter it again, once.
     */
    data object Unreadable : TokenState
}

/**
 * Server address and device token, entered once. They live in the app's private preferences and survive restarts,
 * app updates and changes of source. The token is stored only encrypted, never leaves the device in clear and is
 * excluded from backups (allowBackup=false). Names of the preference file, keys and Keystore alias are those of
 * the previous app versions, so an in-place update keeps the configuration.
 */
class SecureSettings(private val prefs: SharedPreferences, private val cipher: TokenCipher) {
    constructor(context: Context) : this(
        context.applicationContext.getSharedPreferences("moscorun-server", Context.MODE_PRIVATE), KeystoreTokenCipher())

    val serverUrl: String? get() = prefs.getString(URL, null)
    val configured: Boolean get() = serverUrl != null && prefs.contains(TOKEN)

    /** Stores a new server and token. Throws IllegalArgumentException with a message for the user when they are not valid. */
    fun save(url: String, token: String) {
        val normalized = MoscoRunServer.normalizeBaseUrl(url)
        prefs.edit().putString(URL, normalized).putString(TOKEN, sealed(token)).commit()
    }

    /** Replaces the token of the configured server, e.g. after it was revoked from the dashboard. */
    fun replaceToken(token: String) {
        checkNotNull(serverUrl) { "Server non configurato." }
        prefs.edit().putString(TOKEN, sealed(token)).commit()
    }

    fun tokenState(): TokenState {
        val stored = prefs.getString(TOKEN, null) ?: return TokenState.Missing
        return try {
            val token = cipher.open(Base64.getDecoder().decode(stored)).decodeToString()
            if (MoscoRunServer.validToken(token)) TokenState.Available(token) else TokenState.Unreadable
        } catch (_: Exception) {
            // Any Keystore or format failure means the same thing to the user: the token must be entered again.
            TokenState.Unreadable
        }
    }

    /** Shown in the settings instead of the token. */
    fun maskedToken(): String = when (val state = tokenState()) {
        is TokenState.Available -> "mr1_••••" + state.token.takeLast(4)
        TokenState.Unreadable -> "non leggibile"
        TokenState.Missing -> "assente"
    }

    fun forget() {
        prefs.edit().clear().commit()
        runCatching { cipher.forget() }
    }

    private fun sealed(token: String): String {
        val trimmed = token.trim()
        require(MoscoRunServer.validToken(trimmed)) { "Il token deve iniziare con mr1_ (copiarlo dal cruscotto, sezione Dispositivi)." }
        // Standard Base64 without line breaks: the same form the previous versions wrote.
        return Base64.getEncoder().encodeToString(cipher.seal(trimmed.toByteArray()))
    }

    private companion object {
        const val URL = "url"
        const val TOKEN = "token"
    }
}
