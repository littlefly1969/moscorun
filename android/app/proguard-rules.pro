# R8 della build release di MoscoRun. Le librerie (AndroidX, WorkManager, Health Connect, Play Services Maps, Gson)
# portano le proprie regole consumer; qui solo ciò che è specifico dell'app.

# Il sorgente è pubblico: rinominare non protegge nulla e renderebbe illeggibili le segnalazioni d'errore che
# l'app copia negli appunti per chi la installa. Restano rimozione del codice inutilizzato e ottimizzazioni.
-dontobfuscate
-keepattributes SourceFile,LineNumberTable

# Contratto JSON con il server: Gson legge i nomi dei campi per riflessione. Verificato da WireFormatTest
# (classi marcate) e da scripts/check-r8.py (nessun campo rimosso nell'output di R8).
-keepclassmembers @it.moscorun.app.server.WireFormat class * {
    <fields>;
}

# Samsung Health Data SDK: AAR proprietario senza regole consumer documentate e con risoluzione interna dei tipi
# di dato. Mantenuto per intero finché Samsung non pubblica regole ufficiali.
-keep class com.samsung.android.sdk.health.data.** { *; }
-keep interface com.samsung.android.sdk.health.data.** { *; }

# Rapporti per la verifica in CI: percorsi relativi a questo file (app/).
-printusage build/outputs/r8/release/usage.txt
-printconfiguration build/outputs/r8/release/configuration.txt
