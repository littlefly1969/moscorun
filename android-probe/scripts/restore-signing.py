#!/usr/bin/env python3
"""Restore an existing signing key; never create or replace the app identity."""
import argparse
import base64
import hashlib
import json
import os
from pathlib import Path
import re
import ssl
import subprocess
import tempfile


def restore(bundle_text: str, directory: Path, identity: dict) -> Path:
    try:
        bundle = json.loads(bundle_text)
        if not isinstance(bundle, dict) or bundle.get("formatVersion") != 1:
            raise ValueError()
        if bundle.get("keyAlias") != identity["keyAlias"]:
            raise ValueError()
        for key in ("storePassword", "keyPassword"):
            if not re.fullmatch(r"[A-Za-z0-9_-]{32,128}", bundle.get(key, "")):
                raise ValueError()
        keystore = base64.b64decode(bundle["keystoreBase64"], validate=True)
        if not 1024 <= len(keystore) <= 32768:
            raise ValueError()
    except (ValueError, TypeError, KeyError):
        raise ValueError("Bundle firma assente o non valido; nessuna nuova chiave generata.") from None

    directory = directory.resolve()
    if any(c in str(directory) for c in "\n\r"):
        raise ValueError("Percorso firma non valido.")
    directory.mkdir(parents=True, exist_ok=True, mode=0o700)
    directory.chmod(0o700)
    # Check the certificate and both passwords before writing a usable config.
    with tempfile.TemporaryDirectory(dir=directory) as temporary:
        candidate = Path(temporary) / "candidate.p12"
        candidate.write_bytes(keystore)
        candidate.chmod(0o600)
        env = dict(os.environ, MOSCORUN_KEYSTORE_PASSWORD=bundle["storePassword"],
                   MOSCORUN_KEY_PASSWORD=bundle["keyPassword"])
        result = subprocess.run(
            ["keytool", "-list", "-rfc", "-keystore", str(candidate),
             "-storetype", "PKCS12", "-storepass:env", "MOSCORUN_KEYSTORE_PASSWORD",
             "-alias", bundle["keyAlias"]],
            env=env, capture_output=True, text=True, check=False,
        )
        match = re.search(r"-----BEGIN CERTIFICATE-----.*?-----END CERTIFICATE-----",
                          result.stdout, re.DOTALL)
        if result.returncode or not match:
            raise ValueError("Impossibile aprire il certificato di firma.")
        der = ssl.PEM_cert_to_DER_cert(match.group(0))
        fingerprint = hashlib.sha256(der).hexdigest().upper()
        if fingerprint != identity["sha256"].replace(":", "").upper():
            raise ValueError("Certificato diverso dall'identità MoscoRun: ripristino interrotto.")
        # Importing into a throwaway keystore checks access to the private key too.
        result = subprocess.run(
            ["keytool", "-importkeystore", "-noprompt", "-srckeystore", str(candidate),
             "-srcstoretype", "PKCS12", "-srcstorepass:env", "MOSCORUN_KEYSTORE_PASSWORD",
             "-srckeypass:env", "MOSCORUN_KEY_PASSWORD", "-srcalias", bundle["keyAlias"],
             "-destkeystore", str(Path(temporary) / "check.p12"), "-deststoretype", "PKCS12",
             "-deststorepass:env", "MOSCORUN_KEYSTORE_PASSWORD"],
            env=env, capture_output=True, text=True, check=False,
        )
        if result.returncode:
            raise ValueError("Impossibile aprire la chiave privata di firma.")

    target = directory / "moscorun.p12"
    if target.exists() and target.read_bytes() != keystore:
        raise ValueError("La cartella contiene già un keystore differente; nessuna sovrascrittura.")
    target.write_bytes(keystore)
    target.chmod(0o600)
    escaped_path = str(target).replace("\\", "\\\\").replace(":", "\\:").replace(" ", "\\ ")
    properties = directory / "signing.properties"
    properties.write_text(
        f"storeFile={escaped_path}\nstorePassword={bundle['storePassword']}\n"
        f"keyAlias={bundle['keyAlias']}\nkeyPassword={bundle['keyPassword']}\n"
    )
    properties.chmod(0o600)
    return properties


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--bundle", type=Path, help="File riservato; altrimenti usa MOSCORUN_SIGNING_BUNDLE")
    parser.add_argument("--directory", type=Path, required=True)
    args = parser.parse_args()
    try:
        text = args.bundle.read_text() if args.bundle else os.environ.get("MOSCORUN_SIGNING_BUNDLE", "")
        identity = json.loads((Path(__file__).resolve().parents[1] / "signing/identity.json").read_text())
        properties = restore(text, args.directory, identity)
    except (ValueError, OSError):
        # Never include parser/subprocess errors that could echo secret values.
        print("Firma non ripristinata. Controllare bundle, certificato e disponibilità di keytool.")
        return 1
    print(properties)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
