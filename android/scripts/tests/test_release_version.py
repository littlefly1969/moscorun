import importlib.util
from pathlib import Path
import tempfile
import unittest

_spec = importlib.util.spec_from_file_location("release_version", Path(__file__).resolve().parents[1] / "release-version.py")
release = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(release)


class VersionTest(unittest.TestCase):
    def test_name_and_code_come_from_the_tag(self):
        self.assertEqual(("0.8.0", 800_000), release.version("v0.8.0", 0))
        self.assertEqual(("1.2.3", 10_203_004), release.version("v1.2.3", 4))

    def test_codes_grow_with_the_semantic_version(self):
        tags = ["v0.7.0", "v0.7.1", "v0.7.99", "v0.8.0", "v0.99.99", "v1.0.0", "v1.0.1", "v2.0.0", "v209.99.99"]
        codes = [release.version(tag, 999)[1] for tag in tags]
        self.assertEqual(sorted(codes), codes)
        self.assertEqual(len(set(codes)), len(codes))
        self.assertLess(release.version("v0.7.0", 999)[1], release.version("v0.7.1", 0)[1])

    def test_every_official_release_installs_over_the_debug_builds_already_distributed(self):
        self.assertGreater(release.version("v0.1.0", 0)[1], 1101)

    def test_the_android_limit_is_respected(self):
        self.assertLessEqual(release.version("v209.99.99", 999)[1], release.MAX_VERSION_CODE)
        with self.assertRaisesRegex(release.Invalid, "fuori intervallo"):
            release.version("v210.0.1", 0)

    def test_malformed_tags_are_rejected(self):
        for tag in ["0.8.0", "v0.8", "v0.100.0", "v0.1.100", "v01.2.3", "v0.8.0-rc.1", "v0.8.0\n", "", "latest"]:
            with self.subTest(tag=tag), self.assertRaisesRegex(release.Invalid, "Tag di rilascio non valido"):
                release.version(tag, 0)

    def test_the_build_number_is_bounded(self):
        for build in (-1, 1000):
            with self.subTest(build=build), self.assertRaisesRegex(release.Invalid, "build"):
                release.version("v0.8.0", build)

    def test_a_code_not_above_the_last_distributed_is_rejected(self):
        self.assertEqual(800_001, release.version("v0.8.0", 1, floor=800_000)[1])
        for floor in (800_000, 900_000):
            with self.subTest(floor=floor), self.assertRaisesRegex(release.Invalid, "non supera"):
                release.version("v0.8.0", 0, floor=floor)

    def test_the_cli_exports_name_and_code_for_the_next_steps(self):
        with tempfile.NamedTemporaryFile("r", suffix=".env") as env:
            self.assertEqual(0, release.main(["--tag", "v0.8.0", "--build", "2", "--github-env", env.name]))
            self.assertEqual("MOSCORUN_VERSION_NAME=0.8.0\nMOSCORUN_VERSION_CODE=800002\n", env.read())

    def test_the_cli_fails_without_writing_anything(self):
        with tempfile.NamedTemporaryFile("r", suffix=".env") as env:
            self.assertEqual(1, release.main(["--tag", "v0.8", "--github-env", env.name]))
            self.assertEqual("", env.read())


if __name__ == "__main__":
    unittest.main()
