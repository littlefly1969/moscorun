#!/usr/bin/env python3
"""Android version of an official build, derived from the source release tag vX.Y.Z (Conventional Commits).

versionName = X.Y.Z, versionCode = X*10_000_000 + Y*100_000 + Z*1_000 + build. The code grows with the
semantic version, so a later release always installs over an earlier one; `build` (0-999) only serves to
rebuild the same source tag (for instance after a change to the private build) without reusing a code.
"""
import argparse
import re
import sys

TAG = re.compile(r"v(0|[1-9]\d{0,2})\.(0|[1-9]\d?)\.(0|[1-9]\d?)")
MAX_VERSION_CODE = 2_100_000_000  # Google Play and Android limit


class Invalid(Exception):
    """The tag or the build number cannot produce an installable version; the message says why."""


def version(tag: str, build: int, floor: int = 0) -> tuple[str, int]:
    match = TAG.fullmatch(tag)
    if not match:
        raise Invalid(f"Tag di rilascio non valido: {tag!r}. Atteso vX.Y.Z con minor e patch fino a 99.")
    if not 0 <= build <= 999:
        raise Invalid(f"Numero di build fuori intervallo (0-999): {build}")
    major, minor, patch = (int(part) for part in match.groups())
    code = major * 10_000_000 + minor * 100_000 + patch * 1_000 + build
    if not 1 <= code <= MAX_VERSION_CODE:
        raise Invalid(f"versionCode fuori intervallo: {code}")
    if code <= floor:
        raise Invalid(f"versionCode {code} non supera l'ultimo distribuito ({floor}): Android rifiuterebbe "
                      "l'aggiornamento. Usare un tag più recente o un numero di build maggiore.")
    return f"{major}.{minor}.{patch}", code


def main(argv=None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--tag", required=True, help="tag del sorgente, es. v0.8.0")
    parser.add_argument("--build", type=int, default=0, help="ricompilazione dello stesso tag (0-999)")
    parser.add_argument("--floor", type=int, default=0, help="ultimo versionCode distribuito")
    parser.add_argument("--github-env", help="file GITHUB_ENV in cui esportare nome e codice")
    args = parser.parse_args(argv)
    try:
        name, code = version(args.tag, args.build, args.floor)
    except Invalid as error:
        print(f"::error::{error}", file=sys.stderr)
        return 1
    if args.github_env:
        with open(args.github_env, "a", encoding="utf-8") as env:
            env.write(f"MOSCORUN_VERSION_NAME={name}\nMOSCORUN_VERSION_CODE={code}\n")
    print(f"versionName={name} versionCode={code}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
