package it.moscorun.app.ui

import android.app.Activity
import android.os.Bundle
import android.widget.ScrollView
import android.widget.TextView

/** What the app reads, where it sends it and how to stop it. Also shown by Health Connect as the permission rationale. */
class PrivacyActivity : Activity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContentView(ScrollView(this).apply {
            setBackgroundColor(Palette.background)
            addView(TextView(this@PrivacyActivity).apply {
                setPadding(dp(18), dp(40), dp(18), dp(24))
                textSize = 16f
                setTextColor(Palette.ink)
                text = TEXT
            })
        })
    }

    private companion object {
        const val TEXT = "MoscoRun — dati e riservatezza\n\n" +
            "Cosa legge. Solo gli allenamenti registrati sul telefono, con percorso GPS e serie (frequenza cardiaca, velocità, " +
            "cadenza, potenza), da Samsung Health tramite il Samsung Health Data SDK oppure da Health Connect, secondo la " +
            "sorgente che scegli. Se lo consenti, legge anche lo storico scientifico da Health Connect e Samsung Health Data SDK: " +
            "battito, sonno, composizione corporea, attività, nutrizione, pressione, ossigenazione e altri parametri esposti; ognuno " +
            "è facoltativo. Usa soltanto permessi di lettura: non modifica e non cancella nulla nella sorgente.\n\n" +
            "Dove li invia. Soltanto al server MoscoRun che hai configurato tu, in HTTPS, con un token del telefono che puoi " +
            "revocare dal cruscotto. Il token è cifrato sul telefono con una chiave dell'Android Keystore e non viene mai " +
            "mostrato per intero. L'invio in background avviene solo se lo attivi nelle Impostazioni.\n\n" +
            "Cosa resta sul telefono. Un registro tecnico della sincronizzazione (identificativi, impronte, date ed esiti), " +
            "senza misure di salute. Gli allenamenti mostrati nell'app sono letti dal tuo server al momento e non vengono salvati.\n\n" +
            "Mappe. Aprendo la mappa interattiva di un percorso l'app si collega a Google per scaricare la cartografia " +
            "dell'area; prima viene mostrato un avviso. L'app non usa la posizione del telefono.\n\n" +
            "Nessuna pubblicità, nessun servizio di analisi, nessun invio a terzi o a sistemi di intelligenza artificiale.\n\n" +
            "Per smettere: spegni la sincronizzazione automatica, usa \"Scollega\" nelle Impostazioni, revoca il token dal " +
            "cruscotto, oppure revoca i permessi nelle impostazioni di Samsung Health o di Health Connect.\n\n" +
            "MoscoRun non è un dispositivo medico e non fornisce indicazioni mediche."
    }
}
