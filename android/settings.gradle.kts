pluginManagement { repositories { google(); mavenCentral(); gradlePluginPortal() } }
dependencyResolutionManagement { repositoriesMode.set(RepositoriesMode.FAIL_ON_PROJECT_REPOS); repositories { google(); mavenCentral() } }
rootProject.name = "MoscoRun"
include(":app")

// Optional watch application; never adds an APK to the existing official phone workflow.
if (providers.gradleProperty("moscorun.watch").orNull == "true") include(":watch")
