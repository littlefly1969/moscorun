package it.moscorun.probe

import android.app.Activity
import android.widget.Toast

fun canOpenGoogleRouteMap() = false

fun openGoogleMapPreview(activity: Activity) {
    Toast.makeText(activity, "Le mappe Google sono disponibili nella versione completa MoscoRun.", Toast.LENGTH_LONG).show()
}

fun openGoogleRouteMap(activity: Activity, workout: Workout) {
    Toast.makeText(activity, "Le mappe Google sono disponibili nella versione completa MoscoRun.", Toast.LENGTH_LONG).show()
}
