import importlib.util
from pathlib import Path
import unittest

_spec = importlib.util.spec_from_file_location("verify_apks", Path(__file__).resolve().parents[1] / "verify-apks.py")
verify = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(verify)

IDENTITY = {"name": "MoscoRun", "applicationId": "it.littlefly.moscorun", "sha1": "AA", "sha256": "AB:CD"}
CERTS = "Signer #1 certificate SHA-256 digest: abcd\n"
MAPS_KEY = "AIza" + "x" * 35


def badging(package="it.littlefly.moscorun", label="MoscoRun", extra=(), debuggable=False):
    lines = [f"package: name='{package}' versionCode='42' versionName='0.5.0'", f"application-label:'{label}'",
             "uses-permission: name='android.permission.INTERNET'"] + (["application-debuggable"] if debuggable else [])
    lines += [f"uses-permission: name='{permission}'" for permission in extra]
    return "\n".join(lines) + "\n"


def manifest(key=MAPS_KEY, activity=verify.ROUTE_MAP_ACTIVITY, work=True):
    return (f'A: android:name="com.google.android.geo.API_KEY"\nA: android:value="{key}"\nA: android:name="{activity}"\n'
            + ('A: android:name="androidx.work.impl.background.systemjob.SystemJobService"\n' if work else ""))


class SelectApkTest(unittest.TestCase):
    def test_exactly_one_apk_is_accepted(self):
        self.assertEqual("a.apk", verify.select_apk(["a.apk"]))

    def test_no_apk_is_rejected(self):
        with self.assertRaises(verify.Rejected):
            verify.select_apk([])

    def test_a_second_variant_is_rejected(self):
        with self.assertRaisesRegex(verify.Rejected, "un solo APK"):
            verify.select_apk(["debug/app-debug.apk", "connectOnly/debug/app-connectOnly-debug.apk"])


class CheckApkTest(unittest.TestCase):
    def check(self, certs=CERTS, badge=None, xml=None, version="42", name="0.5.0"):
        return verify.check_apk("app-release.apk", certs, badge or badging(), xml or manifest(), IDENTITY, version, name)

    def test_the_single_moscorun_apk_passes(self):
        result = self.check()
        self.assertEqual("it.littlefly.moscorun", result["applicationId"])
        self.assertEqual("MoscoRun", result["label"])
        self.assertEqual(42, result["versionCode"])

    def test_the_report_never_contains_the_maps_key(self):
        self.assertNotIn(MAPS_KEY, str(self.check()))

    def test_the_old_health_connect_variant_is_rejected(self):
        with self.assertRaisesRegex(verify.Rejected, "Identificativo inatteso"):
            self.check(badge=badging(package="it.littlefly.moscorun.hc"))

    def test_another_label_is_rejected(self):
        with self.assertRaisesRegex(verify.Rejected, "Etichetta inattesa"):
            self.check(badge=badging(label="MoscoRun HC"))

    def test_another_certificate_is_rejected(self):
        with self.assertRaisesRegex(verify.Rejected, "Firma inattesa"):
            self.check(certs="Signer #1 certificate SHA-256 digest: ffff\n")

    def test_two_signers_are_rejected(self):
        with self.assertRaises(verify.Rejected):
            self.check(certs=CERTS + "Signer #2 certificate SHA-256 digest: abcd\n")

    def test_an_unexpected_version_is_rejected(self):
        with self.assertRaisesRegex(verify.Rejected, "Versione inattesa"):
            self.check(version="43")

    def test_an_unexpected_version_name_is_rejected(self):
        with self.assertRaisesRegex(verify.Rejected, "Nome di versione inatteso"):
            self.check(name="0.5.1")

    def test_a_debuggable_build_is_never_distributed(self):
        with self.assertRaisesRegex(verify.Rejected, "build di debug"):
            self.check(badge=badging(debuggable=True))

    def test_location_foreground_service_and_exact_alarms_are_rejected(self):
        for permission in verify.FORBIDDEN_PERMISSIONS:
            with self.subTest(permission), self.assertRaisesRegex(verify.Rejected, "Permesso non previsto"):
                self.check(badge=badging(extra=[permission]))

    def test_health_permissions_are_allowed(self):
        self.check(badge=badging(extra=["android.permission.health.READ_EXERCISE"]))

    def test_a_missing_or_placeholder_maps_key_is_rejected(self):
        for key in ("MAPS_KEY_NOT_CONFIGURED", ""):
            with self.subTest(key), self.assertRaisesRegex(verify.Rejected, "Maps"):
                self.check(xml=manifest(key=key))

    def test_the_map_screen_and_the_background_work_must_be_present(self):
        with self.assertRaisesRegex(verify.Rejected, "mappa"):
            self.check(xml=manifest(activity="it.moscorun.probe.RouteMapActivity"))
        with self.assertRaisesRegex(verify.Rejected, "WorkManager"):
            self.check(xml=manifest(work=False))


if __name__ == "__main__":
    unittest.main()
