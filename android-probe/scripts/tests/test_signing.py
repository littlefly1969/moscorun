import base64
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import subprocess
import tempfile
import unittest

spec = importlib.util.spec_from_file_location("restore_signing", Path(__file__).parents[1] / "restore-signing.py")
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)


class SigningTest(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.temp = tempfile.TemporaryDirectory()
        cls.root = Path(cls.temp.name)
        cls.password = "test-only-password-0000000000000000000000"
        env = dict(os.environ, TEST_SIGNING_PASSWORD=cls.password)
        key = cls.root / "test.p12"
        subprocess.run(["keytool", "-genkeypair", "-alias", "moscorun", "-keyalg", "RSA", "-keysize", "2048",
                        "-validity", "365", "-dname", "CN=Temporary test", "-keystore", str(key),
                        "-storetype", "PKCS12", "-storepass:env", "TEST_SIGNING_PASSWORD"],
                       env=env, check=True, capture_output=True)
        cert = subprocess.check_output(["keytool", "-exportcert", "-alias", "moscorun", "-keystore", str(key),
                                        "-storepass:env", "TEST_SIGNING_PASSWORD"], env=env)
        cls.identity = {"keyAlias": "moscorun", "sha256": hashlib.sha256(cert).hexdigest().upper()}
        cls.bundle = {"formatVersion": 1, "keyAlias": "moscorun", "storePassword": cls.password,
                      "keyPassword": cls.password, "keystoreBase64": base64.b64encode(key.read_bytes()).decode()}

    @classmethod
    def tearDownClass(cls):
        cls.temp.cleanup()

    def setUp(self):
        self.output = Path(tempfile.mkdtemp(dir=self.root))

    def test_two_clean_build_environments_restore_identical_key(self):
        a = module.restore(json.dumps(self.bundle), self.output / "a", self.identity)
        b = module.restore(json.dumps(self.bundle), self.output / "b", self.identity)
        self.assertEqual((a.parent / "moscorun.p12").read_bytes(), (b.parent / "moscorun.p12").read_bytes())
        self.assertEqual(a.stat().st_mode & 0o777, 0o600)
        self.assertEqual(a.parent.stat().st_mode & 0o777, 0o700)

    def test_missing_secret_never_generates_key(self):
        with self.assertRaisesRegex(ValueError, "nessuna nuova chiave"):
            module.restore("", self.output, self.identity)
        self.assertFalse((self.output / "moscorun.p12").exists())

    def test_wrong_certificate_rejected_before_config_written(self):
        identity = dict(self.identity, sha256="00" * 32)
        with self.assertRaisesRegex(ValueError, "Certificato diverso"):
            module.restore(json.dumps(self.bundle), self.output, identity)
        self.assertFalse((self.output / "signing.properties").exists())

    def test_wrong_password_rejected(self):
        bundle = dict(self.bundle, storePassword="wrong-password-0000000000000000000000")
        with self.assertRaisesRegex(ValueError, "Impossibile aprire"):
            module.restore(json.dumps(bundle), self.output, self.identity)
        self.assertFalse((self.output / "signing.properties").exists())

    def test_invalid_base64_rejected(self):
        with self.assertRaisesRegex(ValueError, "non valido"):
            module.restore(json.dumps(dict(self.bundle, keystoreBase64="not base64")), self.output, self.identity)

    def test_existing_different_keystore_not_overwritten(self):
        target = self.output / "moscorun.p12"
        target.write_bytes(b"keep-existing-file")
        with self.assertRaisesRegex(ValueError, "nessuna sovrascrittura"):
            module.restore(json.dumps(self.bundle), self.output, self.identity)
        self.assertEqual(target.read_bytes(), b"keep-existing-file")


if __name__ == "__main__":
    unittest.main()
