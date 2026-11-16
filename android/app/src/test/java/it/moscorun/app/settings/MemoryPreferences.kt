package it.moscorun.app.settings

import android.content.SharedPreferences

/** In-memory preferences: the classes under test only need the interface, not a phone. */
class MemoryPreferences : SharedPreferences {
    val values = LinkedHashMap<String, Any?>()

    override fun getAll(): MutableMap<String, *> = LinkedHashMap(values)
    override fun getString(key: String, default: String?): String? = values[key] as? String ?: default
    @Suppress("UNCHECKED_CAST")
    override fun getStringSet(key: String, default: MutableSet<String>?): MutableSet<String>? = values[key] as? MutableSet<String> ?: default
    override fun getInt(key: String, default: Int): Int = values[key] as? Int ?: default
    override fun getLong(key: String, default: Long): Long = values[key] as? Long ?: default
    override fun getFloat(key: String, default: Float): Float = values[key] as? Float ?: default
    override fun getBoolean(key: String, default: Boolean): Boolean = values[key] as? Boolean ?: default
    override fun contains(key: String): Boolean = values.containsKey(key)
    override fun edit(): SharedPreferences.Editor = Editor()
    override fun registerOnSharedPreferenceChangeListener(listener: SharedPreferences.OnSharedPreferenceChangeListener?) {}
    override fun unregisterOnSharedPreferenceChangeListener(listener: SharedPreferences.OnSharedPreferenceChangeListener?) {}

    private inner class Editor : SharedPreferences.Editor {
        private val pending = LinkedHashMap<String, Any?>()
        private val removed = HashSet<String>()
        private var clear = false

        private fun put(key: String, value: Any?) = apply { pending[key] = value; removed.remove(key) }
        override fun putString(key: String, value: String?) = put(key, value)
        override fun putStringSet(key: String, value: MutableSet<String>?) = put(key, value)
        override fun putInt(key: String, value: Int) = put(key, value)
        override fun putLong(key: String, value: Long) = put(key, value)
        override fun putFloat(key: String, value: Float) = put(key, value)
        override fun putBoolean(key: String, value: Boolean) = put(key, value)
        override fun remove(key: String) = apply { removed += key; pending.remove(key) }
        override fun clear() = apply { clear = true }

        override fun commit(): Boolean {
            if (clear) values.clear()
            removed.forEach(values::remove)
            pending.forEach { (key, value) -> if (value == null) values.remove(key) else values[key] = value }
            return true
        }

        override fun apply() { commit() }
    }
}

/** Reversible stand-in for the Keystore cipher; [broken] simulates a key lost after a device restore. */
class FakeCipher : TokenCipher {
    var broken = false
    var forgotten = false

    override fun seal(plain: ByteArray): ByteArray = plain.reversedArray()
    override fun open(sealed: ByteArray): ByteArray {
        if (broken) throw java.security.GeneralSecurityException("chiave non disponibile")
        return sealed.reversedArray()
    }
    override fun forget() { forgotten = true }
}

const val TOKEN = "mr1_abcdefghijklmnop1234"
const val OTHER_TOKEN = "mr1_zyxwvutsrqponmlk9876"
const val SERVER = "https://moscorun.example"
