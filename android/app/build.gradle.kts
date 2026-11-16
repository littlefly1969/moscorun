import groovy.json.JsonSlurper
import java.security.KeyStore
import java.security.MessageDigest
import java.util.Properties

plugins {
    id("com.android.application")
    id("org.jetbrains.kotlin.android")
    id("org.jetbrains.kotlin.plugin.parcelize")
}
val signingFile = providers.environmentVariable("MOSCORUN_SIGNING_PROPERTIES").orNull
    ?.let { file(it) } ?: rootProject.file("signing.properties")
val signingProperties = Properties().apply {
    if (signingFile.isFile) signingFile.inputStream().use { load(it) }
}
val hasStableSigning = signingFile.isFile
val mapsProperties = Properties().apply {
    val localFile = rootProject.file("maps.properties")
    if (localFile.isFile) localFile.inputStream().use { load(it) }
}
val googleMapsKey = providers.environmentVariable("GOOGLE_MAPS_ANDROID_API_KEY").orNull
    ?.trim() ?: mapsProperties.getProperty("apiKey", "").trim()
val identity = JsonSlurper().parse(rootProject.file("signing/identity.json")) as Map<*, *>
if (hasStableSigning) {
    listOf("storeFile", "storePassword", "keyAlias", "keyPassword").forEach {
        check(!signingProperties.getProperty(it).isNullOrBlank()) { "Configurazione firma incompleta: $it" }
    }
    val store = KeyStore.getInstance("PKCS12")
    file(signingProperties.getProperty("storeFile")).inputStream().use {
        store.load(it, signingProperties.getProperty("storePassword").toCharArray())
    }
    val alias = signingProperties.getProperty("keyAlias")
    check(alias == identity["keyAlias"] && store.isKeyEntry(alias)) { "Alias firma MoscoRun non valido" }
    val digest = MessageDigest.getInstance("SHA-256").digest(store.getCertificate(alias).encoded)
        .joinToString(":") { "%02X".format(it.toInt() and 0xff) }
    check(digest == identity["sha256"]) { "Certificato diverso dall'identità MoscoRun" }
}
android {
    namespace = "it.moscorun.app"
    compileSdk = 36
    defaultConfig {
        applicationId = "it.littlefly.moscorun"
        minSdk = 29
        targetSdk = 36
        versionCode = providers.gradleProperty("moscorunVersionCode")
            .orElse(providers.environmentVariable("MOSCORUN_VERSION_CODE")).orElse("2").get().toInt()
            .also { check(it in 1..2100000000) { "versionCode fuori intervallo" } }
        versionName = "0.5.0"
        // One application only: the source (Samsung Health Data SDK or Health Connect) is a setting, not a variant.
        manifestPlaceholders["GOOGLE_MAPS_ANDROID_API_KEY"] = googleMapsKey.ifBlank { "MAPS_KEY_NOT_CONFIGURED" }
    }
    signingConfigs {
        if (hasStableSigning) create("stable") {
            storeFile = file(signingProperties.getProperty("storeFile"))
            storePassword = signingProperties.getProperty("storePassword")
            keyAlias = signingProperties.getProperty("keyAlias")
            keyPassword = signingProperties.getProperty("keyPassword")
            storeType = "PKCS12"
        }
    }
    buildTypes {
        getByName("debug") { signingConfig = signingConfigs.findByName("stable") }
        getByName("release") { signingConfig = signingConfigs.findByName("stable") }
    }
    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }
    kotlinOptions { jvmTarget = "17" }
    testOptions {
        unitTests.isReturnDefaultValues = true
        // Robolectric needs the merged manifest and resources to run the SQLite and WorkManager tests.
        unitTests.isIncludeAndroidResources = true
    }
}
val requireStableSigning by tasks.registering {
    doLast {
        check(hasStableSigning) {
            "Firma stabile non configurata. Ripristinare MOSCORUN_SIGNING_BUNDLE; nessuna firma temporanea consentita."
        }
    }
}
val requireGoogleMapsKey by tasks.registering {
    doLast {
        check(googleMapsKey.matches(Regex("AIza[A-Za-z0-9_-]{35}"))) {
            "Chiave Maps Android assente o non valida. Configurare GOOGLE_MAPS_ANDROID_API_KEY."
        }
    }
}
tasks.configureEach {
    // Resources and JVM tests remain available without giving a PR the private key.
    if (name.matches(Regex("(?:package|sign|validateSigning).*(?:Debug|Release)(?:Bundle)?$")) || name == "signingReport") {
        dependsOn(requireStableSigning)
        dependsOn(requireGoogleMapsKey)
    }
}
val samsungSdk = file("libs/samsung-health-data-api.aar")
val requireSamsungSdk by tasks.registering {
    doLast {
        check(samsungSdk.isFile) {
            """
            Manca il Samsung Health Data SDK: ${samsungSdk.relativeTo(rootDir)} non esiste.
            L'app MoscoRun è unica e lo include sempre; l'AAR è proprietario e non fa parte del repository.
            - In locale: scaricare Samsung Health Data SDK 1.1.0 dal portale Samsung Developer con il proprio account,
              accettarne la licenza e copiare l'AAR in android/app/libs/samsung-health-data-api.aar.
            - Su GitHub: il workflow lo scarica dalla release privata samsung-sdk-local e ne verifica l'impronta.
            Dettagli in android/README.md.
            """.trimIndent()
        }
    }
}
tasks.named("preBuild") { dependsOn(requireSamsungSdk) }
dependencies {
    implementation("androidx.activity:activity-ktx:1.10.1")
    implementation("androidx.health.connect:connect-client:1.1.0")
    implementation("androidx.work:work-runtime-ktx:2.10.1")
    implementation("org.jetbrains.kotlinx:kotlinx-coroutines-android:1.10.2")
    implementation("com.google.code.gson:gson:2.13.1")
    // Proprietary, supplied privately at build time (see requireSamsungSdk): never published with the sources.
    implementation(files(samsungSdk))
    implementation("com.google.android.gms:play-services-maps:20.0.0")
    // Maps brings an old Fragment transitively; Activity Result requires a modern version.
    implementation("androidx.fragment:fragment-ktx:1.8.9")
    testImplementation("junit:junit:4.13.2")
    testImplementation("org.robolectric:robolectric:4.16")
    testImplementation("androidx.test:core:1.7.0")
    testImplementation("androidx.work:work-testing:2.10.1")
}
