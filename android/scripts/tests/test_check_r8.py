import importlib.util
from pathlib import Path
import tempfile
import unittest

_spec = importlib.util.spec_from_file_location("check_r8", Path(__file__).resolve().parents[1] / "check-r8.py")
r8 = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(r8)

SOURCE = """package it.moscorun.app.server

@WireFormat data class UploadSample(val time: String, val value: Double)
@WireFormat
data class UploadSeries(val metric: String, val samples: List<UploadSample>)
data class NotSent(val x: Int)
"""
CONFIGURATION = "-dontobfuscate\n" + r8.KEEP_RULE + " {\n    <fields>;\n}\n"


class CheckR8Test(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        package = Path(self.temp.name) / "it/moscorun/app/server"
        package.mkdir(parents=True)
        (package / "Upload.kt").write_text(SOURCE)
        self.sources = [Path(self.temp.name)]

    def test_marked_classes_are_found_on_the_same_or_the_previous_line(self):
        self.assertEqual({"it.moscorun.app.server.UploadSample", "it.moscorun.app.server.UploadSeries"},
                         r8.wire_classes(self.sources))

    def test_removed_getters_and_unmarked_classes_are_fine(self):
        usage = ("it.moscorun.app.server.UploadSample:\n    public final double getValue()\n"
                 "it.moscorun.app.server.NotSent\n"
                 "it.moscorun.app.server.NotSent2:\n    private final int x\n")
        self.assertEqual(2, len(r8.check(self.sources, usage, CONFIGURATION)))

    def test_a_removed_field_is_rejected(self):
        usage = "it.moscorun.app.server.UploadSeries:\n    private final java.lang.String metric\n"
        with self.assertRaisesRegex(r8.Rejected, "campo rimosso 'private final java.lang.String metric'"):
            r8.check(self.sources, usage, CONFIGURATION)

    def test_a_removed_class_is_rejected(self):
        with self.assertRaisesRegex(r8.Rejected, "UploadSample: classe rimossa"):
            r8.check(self.sources, "it.moscorun.app.server.UploadSample\n", CONFIGURATION)

    def test_the_keep_rule_must_be_in_the_effective_configuration(self):
        with self.assertRaisesRegex(r8.Rejected, "non contiene la regola"):
            r8.check(self.sources, "", "-dontobfuscate\n")

    def test_without_marked_classes_the_check_is_not_meaningful(self):
        with tempfile.TemporaryDirectory() as empty, self.assertRaisesRegex(r8.Rejected, "Nessuna classe"):
            r8.check([Path(empty)], "", CONFIGURATION)

    def test_the_cli_reports_missing_r8_output(self):
        with tempfile.TemporaryDirectory() as reports, self.assertRaisesRegex(SystemExit, "Rapporti R8 assenti"):
            r8.main(["--sources", self.temp.name, "--reports", reports])

    def test_the_real_rules_file_contains_the_rule_checked_here(self):
        rules = (Path(__file__).resolve().parents[2] / "app/proguard-rules.pro").read_text()
        self.assertIn(r8.KEEP_RULE, rules)
        self.assertIn("-printusage build/outputs/r8/release/usage.txt", rules)

    def test_the_real_sources_mark_the_upload_body(self):
        classes = r8.wire_classes([Path(__file__).resolve().parents[2] / "app/src/main/java"])
        self.assertIn("it.moscorun.app.server.UploadPayload", classes)


if __name__ == "__main__":
    unittest.main()
