package it.moscorun.probe

import android.app.Activity
import android.os.Bundle
import android.widget.TextView

class PrivacyActivity:Activity() {
    override fun onCreate(savedInstanceState:Bundle?) {
        super.onCreate(savedInstanceState)
        val networkNotice = if(packageManager.checkPermission("android.permission.INTERNET",packageName) == android.content.pm.PackageManager.PERMISSION_GRANTED)
            "Questa variante include il permesso Internet dichiarato dalla libreria ufficiale Samsung."
        else "Questa variante non ha il permesso Internet."
        setContentView(TextView(this).apply {
            setPadding(32,64,32,32);textSize=18f
            text="MoscoRun Probe — dati sul telefono\n\nQuesta versione verifica quali dati sono leggibili da Samsung Health e Health Connect. Usa soltanto permessi di lettura.\n\nI risultati rimangono in memoria fino alla chiusura o alla cancellazione. Il pulsante Esporta salva un file JSON soltanto nella destinazione scelta da te e può includere posizioni GPS precise.\n\nMoscoRun non invia i rapporti a un proprio server o all’AI e non integra pubblicità o servizi di analisi. $networkNotice Nessuna modifica o cancellazione dei dati originali.\n\nPuoi revocare i permessi nelle impostazioni di Health Connect o Samsung Health. L'app è un prototipo tecnico, non un allenatore e non un servizio medico."
        })
    }
}
