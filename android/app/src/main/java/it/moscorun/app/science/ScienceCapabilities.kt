package it.moscorun.app.science

/** Catalog fields per permission group. Separate sources can report the same metric without merging provenance. */
object ScienceCapabilities {
    val healthConnect = setOf(ScienceMetric.RESTING_HEART_RATE, ScienceMetric.HRV_RMSSD, ScienceMetric.SLEEP, ScienceMetric.OXYGEN_SATURATION, ScienceMetric.RESPIRATORY_RATE, ScienceMetric.VO2_MAX, ScienceMetric.HEART_RATE, ScienceMetric.WEIGHT, ScienceMetric.BODY_FAT, ScienceMetric.LEAN_MASS, ScienceMetric.BODY_WATER, ScienceMetric.BONE_MASS, ScienceMetric.BMR, ScienceMetric.HEIGHT, ScienceMetric.BLOOD_PRESSURE, ScienceMetric.BODY_TEMPERATURE, ScienceMetric.BLOOD_GLUCOSE, ScienceMetric.STEPS, ScienceMetric.DISTANCE, ScienceMetric.FLOORS, ScienceMetric.TOTAL_ENERGY, ScienceMetric.ACTIVE_ENERGY, ScienceMetric.NUTRITION, ScienceMetric.HYDRATION)
    val samsung = setOf(ScienceMetric.SAMSUNG_BODY, ScienceMetric.SAMSUNG_HEART, ScienceMetric.SAMSUNG_OXYGEN, ScienceMetric.SAMSUNG_SKIN, ScienceMetric.SAMSUNG_SLEEP, ScienceMetric.SAMSUNG_PRESSURE, ScienceMetric.SAMSUNG_NUTRITION, ScienceMetric.SAMSUNG_WATER, ScienceMetric.SAMSUNG_GLUCOSE, ScienceMetric.SAMSUNG_TEMP, ScienceMetric.SAMSUNG_FLOORS, ScienceMetric.SAMSUNG_ENERGY, ScienceMetric.SAMSUNG_STEPS, ScienceMetric.SAMSUNG_ACTIVITY)

    private val fields = mapOf(
        ScienceMetric.RESTING_HEART_RATE to listOf("restingHeartRate"),
        ScienceMetric.HRV_RMSSD to listOf("hrvRmssd"),
        ScienceMetric.SLEEP to listOf("sleepDuration", "sleepInBedDuration", "sleepStageDuration"),
        ScienceMetric.OXYGEN_SATURATION to listOf("spo2"),
        ScienceMetric.RESPIRATORY_RATE to listOf("respirationRate"),
        ScienceMetric.VO2_MAX to listOf("vo2max"),
        ScienceMetric.HEART_RATE to listOf("heartRate"),
        ScienceMetric.WEIGHT to listOf("weight"),
        ScienceMetric.BODY_FAT to listOf("fatPercentage"),
        ScienceMetric.LEAN_MASS to listOf("fatFreeMass"),
        ScienceMetric.BODY_WATER to listOf("bodyWater"),
        ScienceMetric.BONE_MASS to listOf("boneMass"),
        ScienceMetric.BMR to listOf("basalMetabolicRate"),
        ScienceMetric.HEIGHT to listOf("height"),
        ScienceMetric.BLOOD_PRESSURE to listOf("systolicBloodPressure", "diastolicBloodPressure"),
        ScienceMetric.BODY_TEMPERATURE to listOf("bodyTemperature"),
        ScienceMetric.BLOOD_GLUCOSE to listOf("bloodGlucose"),
        ScienceMetric.STEPS to listOf("steps"),
        ScienceMetric.DISTANCE to listOf("distance"),
        ScienceMetric.FLOORS to listOf("floorsClimbed"),
        ScienceMetric.TOTAL_ENERGY to listOf("energyExpenditure"),
        ScienceMetric.ACTIVE_ENERGY to listOf("activeEnergyExpenditure"),
        ScienceMetric.NUTRITION to listOf("energyIntake", "carbohydrateIntake", "proteinIntake", "fatIntake", "sodiumIntake", "caffeineIntake", "fiberIntake", "sugarIntake", "saturatedFatIntake"),
        ScienceMetric.HYDRATION to listOf("fluidIntake"),
        ScienceMetric.SAMSUNG_BODY to listOf("weight", "height", "basalMetabolicRate", "fatPercentage", "fatMass", "fatFreePercentage", "fatFreeMass", "musclePercentage", "skeletalMusclePercentage", "skeletalMuscleMass", "bodyWaterVolume", "bodyMassIndex"),
        ScienceMetric.SAMSUNG_HEART to listOf("heartRate"),
        ScienceMetric.SAMSUNG_OXYGEN to listOf("spo2"),
        ScienceMetric.SAMSUNG_SKIN to listOf("skinTemperature"),
        ScienceMetric.SAMSUNG_SLEEP to listOf("sleepScore", "sleepDuration", "sleepInBedDuration", "sleepStageDuration"),
        ScienceMetric.SAMSUNG_PRESSURE to listOf("systolicBloodPressure", "diastolicBloodPressure", "meanBloodPressure", "pulseRate"),
        ScienceMetric.SAMSUNG_NUTRITION to listOf("energyIntake", "carbohydrateIntake", "proteinIntake", "fatIntake", "sodiumIntake", "fiberIntake", "sugarIntake", "saturatedFatIntake"),
        ScienceMetric.SAMSUNG_WATER to listOf("fluidIntake"),
        ScienceMetric.SAMSUNG_GLUCOSE to listOf("bloodGlucose"),
        ScienceMetric.SAMSUNG_TEMP to listOf("bodyTemperature"),
        ScienceMetric.SAMSUNG_FLOORS to listOf("floorsClimbed"),
        ScienceMetric.SAMSUNG_ENERGY to listOf("energyScore"),
        ScienceMetric.SAMSUNG_STEPS to listOf("steps"),
        ScienceMetric.SAMSUNG_ACTIVITY to listOf("activeDuration", "activeEnergyExpenditure", "energyExpenditure", "distance"),
    )

    fun fieldsFor(metrics: Set<ScienceMetric>): Map<String, ScienceMetric> =
        metrics.flatMap { group -> fields.getValue(group).map { it to group } }.toMap()

    /** New permissions or mapping revisions trigger an independent historical backfill rather than skipping old data. */
    fun checkpoint(source: String, version: String, availability: ScienceAvailability): String =
        source + ":" + version + ":" + availability.history + ":" +
            availability.metrics.map { it.name }.sorted().joinToString(",")
}
