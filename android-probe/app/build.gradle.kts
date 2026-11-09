plugins {
    id("com.android.application")
    id("org.jetbrains.kotlin.android")
    id("org.jetbrains.kotlin.plugin.parcelize")
}
android {
    namespace = "it.moscorun.probe"
    compileSdk = 36
    defaultConfig {
        applicationId = "it.moscorun.probe"
        minSdk = 29
        targetSdk = 36
        versionCode = 1
        versionName = "0.1.0"
        testInstrumentationRunner = "androidx.test.runner.AndroidJUnitRunner"
    }
    flavorDimensions += "connector"
    productFlavors {
        create("connectOnly") { dimension = "connector"; resValue("string", "app_name", "MoscoRun Probe HC") }
        create("withSamsung") { dimension = "connector"; applicationIdSuffix = ".samsung"; resValue("string", "app_name", "MoscoRun Probe Samsung") }
    }
    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }
    kotlinOptions { jvmTarget = "17" }
    testOptions { unitTests.isReturnDefaultValues = true }
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
