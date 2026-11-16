package it.moscorun.app.settings

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertSame
import org.junit.Assert.assertThrows
import org.junit.Assert.assertTrue
import org.junit.Test

class SecureSettingsTest {
    private val prefs = MemoryPreferences()
    private val cipher = FakeCipher()
    private val settings = SecureSettings(prefs, cipher)

    @Test fun `nothing is configured at the first start`() {
        assertFalse(settings.configured)
        assertNull(settings.serverUrl)
        assertSame(TokenState.Missing, settings.tokenState())
        assertEquals("assente", settings.maskedToken())
    }

    @Test fun `address and token survive a restart of the app`() {
        settings.save("$SERVER/", "  $TOKEN ")

        val afterRestart = SecureSettings(prefs, cipher)

        assertTrue(afterRestart.configured)
        assertEquals(SERVER, afterRestart.serverUrl)
        assertEquals(TOKEN, (afterRestart.tokenState() as TokenState.Available).token)
    }

    @Test fun `the token is never stored or shown in clear`() {
        settings.save(SERVER, TOKEN)

        assertFalse(prefs.values.values.any { it.toString().contains(TOKEN) })
        assertEquals("mr1_••••1234", settings.maskedToken())
    }

    @Test fun `an invalid address or token leaves the working configuration untouched`() {
        settings.save(SERVER, TOKEN)

        assertThrows(IllegalArgumentException::class.java) { settings.save("http://insecure.example", TOKEN) }
        assertThrows(IllegalArgumentException::class.java) { settings.save(SERVER, "non-un-token") }
        assertThrows(IllegalArgumentException::class.java) { settings.replaceToken("") }

        assertEquals(SERVER, settings.serverUrl)
        assertEquals(TOKEN, (settings.tokenState() as TokenState.Available).token)
    }

    @Test fun `replacing the token keeps the address`() {
        settings.save(SERVER, TOKEN)

        settings.replaceToken(OTHER_TOKEN)

        assertEquals(SERVER, settings.serverUrl)
        assertEquals(OTHER_TOKEN, (settings.tokenState() as TokenState.Available).token)
    }

    @Test fun `a lost keystore key asks for the token again without losing the address`() {
        settings.save(SERVER, TOKEN)
        cipher.broken = true

        assertSame(TokenState.Unreadable, settings.tokenState())
        assertTrue(settings.configured)
        assertEquals(SERVER, settings.serverUrl)
        assertEquals("non leggibile", settings.maskedToken())

        cipher.broken = false
        settings.replaceToken(OTHER_TOKEN)
        assertEquals(OTHER_TOKEN, (settings.tokenState() as TokenState.Available).token)
    }

    @Test fun `a stored value that is not a token is treated as unreadable`() {
        prefs.edit().putString("url", SERVER).putString("token", "%%%non-base64%%%").commit()

        assertSame(TokenState.Unreadable, settings.tokenState())
    }

    @Test fun `the configuration written by the previous app versions is read as it is`() {
        // Same file keys and the same Base64 form (standard alphabet, padding, no line breaks).
        val sealed = java.util.Base64.getEncoder().encodeToString(cipher.seal(TOKEN.toByteArray()))
        prefs.edit().putString("url", SERVER).putString("token", sealed).commit()

        assertEquals(TOKEN, (settings.tokenState() as TokenState.Available).token)
    }

    @Test fun `forgetting removes address, token and key`() {
        settings.save(SERVER, TOKEN)

        settings.forget()

        assertFalse(settings.configured)
        assertTrue(prefs.values.isEmpty())
        assertTrue(cipher.forgotten)
    }

    @Test fun `replacing the token needs a configured server`() {
        assertThrows(IllegalStateException::class.java) { settings.replaceToken(TOKEN) }
    }
}
