package it.moscorun.probe

import android.content.Context
import android.security.keystore.KeyGenParameterSpec
import android.security.keystore.KeyProperties
import android.util.Base64
import java.security.KeyStore
import javax.crypto.Cipher
import javax.crypto.KeyGenerator
import javax.crypto.SecretKey
import javax.crypto.spec.GCMParameterSpec

/**
 * Server address and device token. The token is encrypted with an AES-GCM key kept in the Android Keystore:
 * it never leaves the device in clear and is excluded from backups (allowBackup=false).
 */
class SecureSettings(context: Context) {
    private val prefs = context.getSharedPreferences("moscorun-server", Context.MODE_PRIVATE)
    private val alias = "moscorun-device-token"

    val serverUrl: String? get() = prefs.getString("url", null)
    val configured: Boolean get() = serverUrl != null && prefs.contains("token")

    fun save(url: String, token: String) {
        val normalized = MoscoRunServer.normalizeBaseUrl(url)
        require(MoscoRunServer.validToken(token.trim())) { "Il token deve iniziare con mr1_ (copiarlo dal cruscotto, sezione Dispositivi)." }
        val cipher = Cipher.getInstance("AES/GCM/NoPadding").apply { init(Cipher.ENCRYPT_MODE, key()) }
        val sealed = cipher.iv + cipher.doFinal(token.trim().toByteArray())
        prefs.edit().putString("url", normalized).putString("token", Base64.encodeToString(sealed, Base64.NO_WRAP)).apply()
    }

    fun token(): String? {
        val sealed = prefs.getString("token", null)?.let { Base64.decode(it, Base64.NO_WRAP) } ?: return null
        val cipher = Cipher.getInstance("AES/GCM/NoPadding")
        cipher.init(Cipher.DECRYPT_MODE, key(), GCMParameterSpec(128, sealed, 0, 12))
        return cipher.doFinal(sealed, 12, sealed.size - 12).decodeToString()
    }

    fun forget() {
        prefs.edit().clear().apply()
        KeyStore.getInstance("AndroidKeyStore").apply { load(null) }.deleteEntry(alias)
    }

    private fun key(): SecretKey {
        val store = KeyStore.getInstance("AndroidKeyStore").apply { load(null) }
        (store.getKey(alias, null) as? SecretKey)?.let { return it }
        return KeyGenerator.getInstance(KeyProperties.KEY_ALGORITHM_AES, "AndroidKeyStore").apply {
            init(KeyGenParameterSpec.Builder(alias, KeyProperties.PURPOSE_ENCRYPT or KeyProperties.PURPOSE_DECRYPT)
                .setBlockModes(KeyProperties.BLOCK_MODE_GCM)
                .setEncryptionPaddings(KeyProperties.ENCRYPTION_PADDING_NONE)
                .setKeySize(256).build())
        }.generateKey()
    }
}
