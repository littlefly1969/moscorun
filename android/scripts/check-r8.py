#!/usr/bin/env python3
"""Check the R8 output of the release build: no field of a @WireFormat class may be removed.

Gson serialises those classes by reflection, so R8 cannot see that their fields are read: if one were removed
the server would receive JSON without it, and only on the release APK. The keep rule lives in
app/proguard-rules.pro; this check proves that it was applied and that it covers every marked class.
"""
import argparse
from pathlib import Path
import re

MARKER = "it.moscorun.app.server.WireFormat"
KEEP_RULE = f"-keepclassmembers @{MARKER} class *"
_PACKAGE = re.compile(r"^package\s+([\w.]+)", re.MULTILINE)
_MARKED = re.compile(r"@WireFormat\s+(?:(?:data|public|internal|final|open)\s+)*class\s+(\w+)")


class Rejected(Exception):
    """The release build would break the server contract; the message says why."""


def wire_classes(sources):
    """Fully qualified names of the Kotlin classes annotated with @WireFormat."""
    found = set()
    for root in sources:
        for path in sorted(Path(root).rglob("*.kt")):
            text = path.read_text(encoding="utf-8")
            package = _PACKAGE.search(text)
            for name in _MARKED.findall(text):
                found.add(f"{package[1]}.{name}" if package else name)
    return found


def removed_fields(usage: str, classes):
    """Entries of R8's -printusage report that remove a marked class or one of its fields."""
    problems, current = [], None
    for line in usage.splitlines():
        if not line.strip():
            continue
        if not line[0].isspace():
            name = line.strip()
            current = name[:-1] if name.endswith(":") else None
            if not name.endswith(":") and name in classes:
                problems.append(f"{name}: classe rimossa")
        elif current in classes and "(" not in line:
            problems.append(f"{current}: campo rimosso '{line.strip()}'")
    return problems


def check(sources, usage: str, configuration: str):
    classes = wire_classes(sources)
    if not classes:
        raise Rejected("Nessuna classe @WireFormat trovata nei sorgenti: controllo non significativo.")
    if KEEP_RULE not in configuration:
        raise Rejected(f"La configurazione R8 effettiva non contiene la regola '{KEEP_RULE}'.")
    problems = removed_fields(usage, classes)
    if problems:
        raise Rejected("R8 ha rimosso parti del contratto JSON:\n" + "\n".join(problems))
    return sorted(classes)


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--sources", type=Path, nargs="+", default=[Path("app/src/main/java")])
    parser.add_argument("--reports", type=Path, default=Path("app/build/outputs/r8/release"))
    args = parser.parse_args(argv)
    try:
        usage = (args.reports / "usage.txt").read_text(encoding="utf-8")
        configuration = (args.reports / "configuration.txt").read_text(encoding="utf-8")
    except OSError:
        raise SystemExit(f"::error::Rapporti R8 assenti in {args.reports}: eseguire prima minifyReleaseWithR8.")
    try:
        classes = check(args.sources, usage, configuration)
    except Rejected as error:
        raise SystemExit(f"::error::{error}")
    print("Contratto JSON integro dopo R8: " + ", ".join(classes))


if __name__ == "__main__":
    main()
