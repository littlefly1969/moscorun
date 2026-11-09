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
    namespace = "it.moscorun.probe"
    compileSdk = 36
    defaultConfig {
        applicationId = "it.littlefly.moscorun"
        minSdk = 29
        targetSdk = 36
        versionCode = providers.gradleProperty("moscorunVersionCode")
            .orElse(providers.environmentVariable("MOSCORUN_VERSION_CODE")).orElse("2").get().toInt()
            .also { check(it in 1..2100000000) { "versionCode fuori intervallo" } }
        versionName = "0.2.0"
        testInstrumentationRunner = "androidx.test.runner.AndroidJUnitRunner"
    }
    flavorDimensions += "connector"
    productFlavors {
        create("connectOnly") { dimension = "connector"; applicationIdSuffix = ".hc"; resValue("string", "app_name", "MoscoRun HC") }
        create("withSamsung") { dimension = "connector"; resValue("string", "app_name", "MoscoRun") }
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
    testOptions { unitTests.isReturnDefaultValues = true }
}
val requireStableSigning by tasks.registering {
    doLast {
        check(hasStableSigning) {
            "Firma stabile non configurata. Ripristinare MOSCORUN_SIGNING_BUNDLE; nessuna firma temporanea consentita."
        }
    }
}
tasks.configureEach {
    // Resources and JVM tests remain available without giving a PR the private key.
    if (name.matches(Regex("(?:package|sign|validateSigning).*(?:Debug|Release)(?:Bundle)?$")) || name == "signingReport") {
        dependsOn(requireStableSigning)
    }
}
dependencies {
    implementation("androidx.activity:activity-ktx:1.10.1")
    implementation("androidx.lifecycle:lifecycle-runtime-ktx:2.9.1")
    implementation("androidx.health.connect:connect-client:1.1.0")
    implementation("org.jetbrains.kotlinx:kotlinx-coroutines-android:1.10.2")
    implementation("com.google.code.gson:gson:2.13.1")
    add("withSamsungImplementation", files("libs/samsung-health-data-api.aar"))
    testImplementation("junit:junit:4.13.2")
}
tasks.matching { it.name.contains("WithSamsung") && it.name.startsWith("pre") }.configureEach {
    doFirst {
        check(file("libs/samsung-health-data-api.aar").isFile) {
            "Manca l'SDK ufficiale Samsung: leggere android-probe/README.md. Usare assembleConnectOnlyDebug per Health Connect."
        }
    }
}
