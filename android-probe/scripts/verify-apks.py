#!/usr/bin/env python3
"""Check the certificate and identity of every APK before publishing artifacts."""
import argparse
import json
import os
from pathlib import Path
import re
import subprocess


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--sdk", type=Path, default=os.environ.get("ANDROID_HOME"))
    parser.add_argument("--apk-root", type=Path, default=Path("app/build/outputs/apk"))
    parser.add_argument("--report", type=Path, default=Path("app/build/reports/signing/identity.json"))
    args = parser.parse_args()
    identity = json.loads((Path(__file__).resolve().parents[1] / "signing/identity.json").read_text())
    tools = args.sdk / "build-tools/36.0.0"
    apks = sorted(args.apk_root.glob("**/debug/*.apk"))
    if not apks:
        raise SystemExit("Nessun APK da verificare.")
    results = []
    for apk in apks:
        certs = subprocess.check_output([str(tools / "apksigner"), "verify", "--print-certs", str(apk)], text=True)
        digests = re.findall(r"Signer #\d+ certificate SHA-256 digest: ([0-9a-fA-F]+)", certs)
        if digests != [identity["sha256"].replace(":", "").lower()]:
            raise SystemExit(f"Firma inattesa: {apk.name}")
        badging = subprocess.check_output([str(tools / "aapt"), "dump", "badging", str(apk)], text=True)
        package = re.search(r"^package: name='([^']+)' versionCode='(\d+)' versionName='([^']+)'", badging, re.MULTILINE)
        expected = identity["healthConnectApplicationId"] if "connectOnly" in apk.parts else identity["applicationId"]
        if not package or package[1] != expected:
            raise SystemExit(f"Identificativo inatteso: {apk.name}")
        if package[2] != os.environ.get("MOSCORUN_VERSION_CODE", package[2]):
            raise SystemExit(f"Versione inattesa: {apk.name}")
        results.append({"file": apk.name, "applicationId": package[1], "versionCode": int(package[2]),
                        "versionName": package[3], "sha1": identity["sha1"], "sha256": identity["sha256"]})
    args.report.parent.mkdir(parents=True, exist_ok=True)
    args.report.write_text(json.dumps(results, indent=2) + "\n")
    print(json.dumps(results, indent=2))


if __name__ == "__main__":
    main()
