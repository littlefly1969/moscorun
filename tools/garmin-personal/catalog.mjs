export const healthCatalog = Object.freeze({
  "/biometric-service/heartRateZones/v2": {
    query: ["generateDefaults"],
    path: [],
  },
  "/fitnessage-service/fitnessage/v2/{calendarDate}": {
    query: ["fullHistoryScan"],
    path: ["calendarDate"],
  },
  "/goal-service/goal/user/effective/weightgoal/{startDate}/{endDate}": {
    query: [],
    path: ["startDate", "endDate"],
  },
  "/hrv-service/hrv/{calendarDate}": {
    query: [],
    path: ["calendarDate"],
  },
  "/hrv-service/hrv/daily/{fromDate}/{toDate}": {
    query: [],
    path: ["fromDate", "toDate"],
  },
  "/metrics-service/metrics/maxmet/daily/{startDate}/{endDate}": {
    query: ["sport"],
    path: ["startDate", "endDate"],
  },
  "/nutrition-service/food/logs/{date}": {
    query: ["includeGoals", "includeNutrition", "includeMedia", "mealId"],
    path: ["date"],
  },
  "/sleep-service/sleep/dailySleep": {
    query: ["date"],
    path: [],
  },
  "/sleep-service/sleep/dailySleepSummariesByDate": {
    query: ["startDate", "endDate"],
    path: [],
  },
  "/usersummary-service/stats/hydration/daily/{startDate}/{endDate}": {
    query: [],
    path: ["startDate", "endDate"],
  },
  "/usersummary-service/stats/steps/daily/{startDate}/{endDate}": {
    query: [],
    path: ["startDate", "endDate"],
  },
  "/usersummary-service/stats/stress/daily/{startDate}/{endDate}": {
    query: [],
    path: ["startDate", "endDate"],
  },
  "/usersummary-service/usersummary/daily": {
    query: ["calendarDate"],
    path: [],
  },
  "/usersummary-service/v2/dailysummaries": {
    query: ["startDate", "endDate"],
    path: [],
  },
  "/weight-service/weight/all/{startDate}/{endDate}": {
    query: [],
    path: ["startDate", "endDate"],
  },
  "/weight-service/weight/range/{startDate}/{endDate}": {
    query: ["includeAll"],
    path: ["startDate", "endDate"],
  },
  "/wellness-service/wellness/bodyBattery/reports/daily": {
    query: ["startDate", "endDate", "chunks"],
    path: [],
  },
  "/wellness-service/wellness/daily/spo2acclimation/{calendarDate}": {
    query: [],
    path: ["calendarDate"],
  },
  "/wellness-service/wellness/dailyHeartRate": {
    query: ["date"],
    path: [],
  },
});
