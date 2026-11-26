#!/usr/bin/env python3
"""Check that the build produced exactly one MoscoRun APK with the expected identity, before publishing it."""
import argparse
import json
import os
from pathlib import Path
import re
import subprocess

ROUTE_MAP_ACTIVITY = "it.moscorun.app.ui.RouteMapActivity"
FORBIDDEN_PERMISSIONS = (
    # Recorded routes are shown; the phone's own position is never read.
    "android.permission.ACCESS_FINE_LOCATION",
    "android.permission.ACCESS_COARSE_LOCATION",
    "android.permission.ACCESS_BACKGROUND_LOCATION",
    # Background work is deferrable WorkManager work only.
    "android.permission.FOREGROUND_SERVICE",
    "android.permission.SCHEDULE_EXACT_ALARM",
    "android.permission.USE_EXACT_ALARM",
    # Health data is only read.
    "android.permission.WRITE_EXTERNAL_STORAGE",
)


class Rejected(Exception):
    """The artifact must not be published; the message says why."""


def select_apk(apks):
    """The build must leave one APK: a second one would be a variant users could install by mistake."""
    if not apks:
        raise Rejected("Nessun APK da verificare.")
    if len(apks) > 1:
        raise Rejected("Atteso un solo APK MoscoRun, trovati: " + ", ".join(sorted(str(a) for a in apks)))
    return apks[0]


def check_apk(name, certs, badging, manifest, identity, version_code=None, version_name=None):
    """Validate the tool outputs for one APK and return the report entry. Raises Rejected with the reason."""
    digests = re.findall(r"Signer #\d+ certificate SHA-256 digest: ([0-9a-fA-F]+)", certs)
    if digests != [identity["sha256"].replace(":", "").lower()]:
        raise Rejected(f"Firma inattesa: {name}")
    package = re.search(r"^package: name='([^']+)' versionCode='(\d+)' versionName='([^']+)'", badging, re.MULTILINE)
    if not package or package[1] != identity["applicationId"]:
        found = package[1] if package else "illeggibile"
        raise Rejected(f"Identificativo inatteso in {name}: {found}. L'unica app è {identity['applicationId']}.")
    if version_code is not None and package[2] != str(version_code):
        raise Rejected(f"Versione inattesa: {name}")
    if version_name is not None and package[3] != version_name:
        raise Rejected(f"Nome di versione inatteso in {name}: {package[3]}")
    if re.search(r"^application-debuggable", badging, re.MULTILINE):
        raise Rejected(f"{name} è una build di debug: si distribuisce solo la build release.")
    label = re.search(r"^application-label:'([^']*)'", badging, re.MULTILINE)
    if not label or label[1] != identity["name"]:
        found = label[1] if label else "assente"
        raise Rejected(f"Etichetta inattesa in {name}: {found}. Deve essere {identity['name']}.")
    for permission in FORBIDDEN_PERMISSIONS:
        if f"'{permission}'" in badging:
            raise Rejected(f"Permesso non previsto in {name}: {permission}")
    if "android.permission.INTERNET" not in badging:
        raise Rejected("Manca il permesso Internet: l'app non potrebbe raggiungere il server.")
    if "com.google.android.geo.API_KEY" not in manifest or "MAPS_KEY_NOT_CONFIGURED" in manifest \
            or not re.search(r"AIza[A-Za-z0-9_-]{35}", manifest):
        raise Rejected("Configurazione Maps mancante nell'APK.")
    if ROUTE_MAP_ACTIVITY not in manifest:
        raise Rejected("Schermata della mappa mancante nell'APK.")
    if "androidx.work.impl.background.systemjob.SystemJobService" not in manifest:
        raise Rejected("WorkManager mancante nell'APK: la sincronizzazione in background non partirebbe.")
    return {"file": name, "applicationId": package[1], "label": label[1], "versionCode": int(package[2]),
            "versionName": package[3], "sha1": identity["sha1"], "sha256": identity["sha256"],
            "googleMapsConfigured": True}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--sdk", type=Path, default=os.environ.get("ANDROID_HOME"))
    parser.add_argument("--apk-root", type=Path, default=Path("app/build/outputs/apk"))
    parser.add_argument("--report", type=Path, default=Path("app/build/reports/signing/identity.json"))
    args = parser.parse_args()
    identity = json.loads((Path(__file__).resolve().parents[1] / "signing/identity.json").read_text())
    tools = args.sdk / "build-tools/36.0.0"
    try:
        apk = select_apk(sorted(args.apk_root.glob("**/*.apk")))
        certs = subprocess.check_output([str(tools / "apksigner"), "verify", "--print-certs", str(apk)], text=True)
        badging = subprocess.check_output([str(tools / "aapt"), "dump", "badging", str(apk)], text=True)
        manifest = subprocess.check_output(
            [str(tools / "aapt"), "dump", "xmltree", str(apk), "AndroidManifest.xml"], text=True)
        result = check_apk(apk.name, certs, badging, manifest, identity, os.environ.get("MOSCORUN_VERSION_CODE"),
                           os.environ.get("MOSCORUN_VERSION_NAME"))
    except Rejected as error:
        raise SystemExit(f"::error::{error}")
    args.report.parent.mkdir(parents=True, exist_ok=True)
    args.report.write_text(json.dumps([result], indent=2) + "\n")
    # The report holds identifiers and certificate digests only: the Maps key in the manifest is never printed.
    print(json.dumps([result], indent=2))


if __name__ == "__main__":
    main()
