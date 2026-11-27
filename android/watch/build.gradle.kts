plugins {
    id("com.android.application")
    id("org.jetbrains.kotlin.android")
}
val sensorSdk = file("libs/samsung-health-sensor-api.aar")
check(sensorSdk.isFile) {
    "Manca Samsung Health Sensor SDK 1.4.1: fornire privatamente android/watch/libs/samsung-health-sensor-api.aar"
}
android {
    namespace = "it.moscorun.watch"
    compileSdk = 36
    defaultConfig {
        applicationId = "it.littlefly.moscorun.watch"
        minSdk = 30
        targetSdk = 35
        versionCode = 1
        versionName = "0.1.0-dev"
    }
    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }
    kotlinOptions { jvmTarget = "17" }
    sourceSets.getByName("main").java.srcDir("../sensor-core/src/main/java")
}
dependencies {
    implementation(files(sensorSdk))
    implementation("com.google.code.gson:gson:2.13.1")
}
tasks.configureEach {
    if (name.matches(Regex("(?:package|assemble|bundle|install).*Release"))) {
        doFirst { error("Il rilascio Wear OS firmato richiede una procedura privata dedicata; non usare la firma del telefono.") }
    }
}
