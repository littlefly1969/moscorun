package it.moscorun.app.server

/**
 * Marks a class serialised by reflection (Gson) whose field names are the JSON contract with the server.
 * The release build keeps its fields (app/proguard-rules.pro) and `scripts/check-r8.py` fails if R8 removes one;
 * `WireFormatTest` fails if a class reachable from an annotated body lacks this marker.
 */
@Target(AnnotationTarget.CLASS)
@Retention(AnnotationRetention.RUNTIME)
@MustBeDocumented
annotation class WireFormat
