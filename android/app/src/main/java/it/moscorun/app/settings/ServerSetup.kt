package it.moscorun.app.settings

import it.moscorun.app.server.DeviceIdentity
import it.moscorun.app.server.Failure
import it.moscorun.app.server.MoscoRunServer
import it.moscorun.app.server.ReadResult
import it.moscorun.app.server.ServerApi
import it.moscorun.app.sync.SyncRunner
import java.time.Duration
import java.time.Instant

/**
 * The one-time configuration of the server and what can be done to it afterwards. Address and token are checked
 * against the server before anything is stored, so a typing mistake never replaces a working configuration.
 * Every method talks to the network: call them off the main thread.
 */
class ServerSetup(
    private val settings: SecureSettings,
    private val prefs: AppPreferences,
    private val sync: SyncRunner,
    private val server: (url: String, token: String) -> ServerApi,
    private val now: () -> Instant = Instant::now,
) {
    sealed interface Outcome {
        val message: String

        /** The server accepted the token. [identity] is null for a server older than the identity endpoint. */
        data class Connected(val identity: DeviceIdentity?, override val message: String) : Outcome

        /** Nothing was changed. */
        data class Rejected(override val message: String, val unauthorized: Boolean = false) : Outcome
    }

    /** First configuration: stores address and token only when the server accepts them. */
    fun connect(url: String, token: String): Outcome {
        val normalized = try { MoscoRunServer.normalizeBaseUrl(url) } catch (e: IllegalArgumentException) {
            return Outcome.Rejected(e.message ?: "Indirizzo del server non valido.")
        }
        val trimmed = token.trim()
        if (!MoscoRunServer.validToken(trimmed)) return Outcome.Rejected(TOKEN_FORMAT)
        return check(normalized, trimmed) { settings.save(normalized, trimmed) }
    }

    /** Replaces the token of the configured server, for instance after it was revoked from the dashboard. */
    fun replaceToken(token: String): Outcome {
        val url = settings.serverUrl ?: return Outcome.Rejected("Server non configurato.")
        val trimmed = token.trim()
        if (!MoscoRunServer.validToken(trimmed)) return Outcome.Rejected(TOKEN_FORMAT)
        return check(url, trimmed) { settings.replaceToken(trimmed) }
    }

    /** "Verifica connessione": checks the stored configuration without changing it. */
    fun verify(): Outcome {
        val url = settings.serverUrl ?: return Outcome.Rejected("Server non configurato.")
        val token = when (val state = settings.tokenState()) {
            is TokenState.Available -> state.token
            TokenState.Unreadable -> return Outcome.Rejected("Il token salvato non è più leggibile su questo telefono: inseriscilo di nuovo.", unauthorized = true)
            TokenState.Missing -> return Outcome.Rejected("Token assente.")
        }
        val outcome = check(url, token) {}
        if (outcome is Outcome.Rejected && outcome.unauthorized) prefs.authBlocked = true
        return outcome
    }

    private fun check(url: String, token: String, store: () -> Unit): Outcome {
        val api = server(url, token)
        return when (val identity = api.identity()) {
            is ReadResult.Ok -> {
                store()
                sync.adopt(url, identity.value)
                prefs.authBlocked = false
                Outcome.Connected(identity.value, "Connessione riuscita: telefono registrato come \"${identity.value.name}\".")
            }
            is ReadResult.Failed -> when {
                // An address that is not a MoscoRun server answers 404 too: a second, older endpoint tells them apart.
                identity.kind == Failure.NOT_FOUND -> when (val probe = api.workoutsBetween(now().minus(Duration.ofDays(1)), now())) {
                    is ReadResult.Ok -> {
                        store()
                        prefs.authBlocked = false
                        Outcome.Connected(null, "Connessione riuscita. Il server è di una versione precedente: aggiornalo per avere tutte le funzioni dell'app.")
                    }
                    is ReadResult.Failed ->
                        if (probe.kind == Failure.NOT_FOUND) Outcome.Rejected("A questo indirizzo non risponde un server MoscoRun: controlla l'indirizzo.")
                        else rejected(probe)
                }
                else -> rejected(identity)
            }
        }
    }

    private fun rejected(failure: ReadResult.Failed) = Outcome.Rejected(when (failure.kind) {
        Failure.UNAUTHORIZED -> "Il server non riconosce questo token: copialo di nuovo dal cruscotto (Dispositivi) o creane uno nuovo."
        Failure.FORBIDDEN -> "Questo token non è di un telefono: nel cruscotto registra il dispositivo come \"Telefono Android\"."
        Failure.OFFLINE -> "${failure.message} Nulla è stato modificato: riprova quando il server è raggiungibile."
        else -> failure.message
    }, unauthorized = failure.unauthorized)

    private companion object {
        const val TOKEN_FORMAT = "Il token deve iniziare con mr1_ (copialo dal cruscotto, sezione Dispositivi)."
    }
}
