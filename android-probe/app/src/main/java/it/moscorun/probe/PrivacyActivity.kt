package it.moscorun.probe

import android.app.Activity
import android.os.Bundle
import android.widget.TextView

class PrivacyActivity:Activity() {
    override fun onCreate(savedInstanceState:Bundle?) {
        super.onCreate(savedInstanceState)
        setContentView(TextView(this).apply {
            setPadding(32,64,32,32);textSize=18f
            text="MoscoRun Probe — dati sul telefono\n\nQuesta versione verifica quali dati sono leggibili da Samsung Health e Health Connect. Usa soltanto permessi di lettura.\n\nI risultati rimangono in memoria fino alla chiusura o alla cancellazione. Il pulsante Esporta salva un file JSON soltanto nella destinazione scelta da te e può includere posizioni GPS precise.\n\nNessun invio a server, AI, pubblicità o servizi di analisi. Nessun permesso Internet. Nessuna modifica o cancellazione dei dati originali.\n\nPuoi revocare i permessi nelle impostazioni di Health Connect o Samsung Health. L'app è un prototipo tecnico, non un allenatore e non un servizio medico."
        })
    }
}
