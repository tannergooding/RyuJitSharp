"""Compare full helper phases and unmodified method bytes, with a native repeat control."""

import argparse
import difflib
import json
import re
import sys
from datetime import datetime
from pathlib import Path


HEADER = re.compile(r"^\*{6} START compiling (.+?) \(MethodHash=([0-9a-fA-F]+)\)$", re.M)
PHASE_START = re.compile(r"^\*{15} Starting PHASE (.+)$", re.M)
REJECTIONS = (
    "CORJIT_SKIPPED", "Assertion failed", "Assertion Failure",
    "NotYetImplementedException", "NotImplementedException",
)
ALT_SETTINGS = {
    "DOTNET_AltJit", "DOTNET_AltJitName", "DOTNET_AltJitPath", "DOTNET_RunAltJitCode",
}


def parse_dump(text, cohort):
    text = text.replace("\r\n", "\n")
    errors = []
    methods = {}
    headers = list(HEADER.finditer(text))
    expected = cohort["expectedMethods"]
    found = []
    for index, header in enumerate(headers):
        identity = header[1]
        match = re.match(re.escape(cohort["typeName"]) + r":([^(\[]+)(?:\[.*?\])?\(", identity)
        if match is None or match[1] not in expected:
            errors.append({"kind": "unexpected-method", "identity": identity})
            continue
        name = match[1]
        found.append(name)
        if name in methods:
            errors.append({"kind": "duplicate-method", "identity": identity})
            continue
        end = headers[index + 1].start() if index + 1 < len(headers) else len(text)
        body = text[header.start():end]
        method = {"identity": identity, "hash": header[2], "phase": None, "bytes": None}
        starts = list(PHASE_START.finditer(body))
        selected = [i for i, start in enumerate(starts) if start[1] == cohort["phase"]]
        if len(selected) != 1 or selected[0] + 1 >= len(starts):
            errors.append({"kind": "phase-boundary", "method": identity, "matches": len(selected)})
        else:
            i = selected[0]
            # Include the finishing marker and all post-phase trees/checks until the next phase.
            phase = body[starts[i].start():starts[i + 1].start()]
            finish = re.compile(
                r"^\*{15} Finishing PHASE " + re.escape(cohort["phase"]) + r"(?: \[.*\])?$", re.M
            )
            if len(finish.findall(phase)) != 1:
                errors.append({"kind": "phase-finish", "method": identity})
            else:
                method["phase"] = phase
        raw_headers = list(re.finditer(r"^Generated native code for (.+):\n", body, re.M))
        if len(raw_headers) != 1 or raw_headers[0][1] != identity:
            errors.append({"kind": "raw-code-identity", "method": identity})
        else:
            line = body[raw_headers[0].end():].split("\n", 1)[0]
            if not re.fullmatch(r"(?:[0-9a-fA-F]{2})+", line):
                errors.append({"kind": "raw-code-format", "method": identity, "line": line})
            else:
                raw = bytes.fromhex(line)
                sizes = re.findall(r"^Method code size: (\d+)$", body, re.M)
                if len(sizes) != 1 or int(sizes[0]) != len(raw):
                    # JitRawHexCode dumps hot code only. Do not pass an incomplete cold section.
                    errors.append({
                        "kind": "raw-code-coverage", "method": identity,
                        "rawBytes": len(raw), "reportedSizes": sizes,
                    })
                else:
                    method["bytes"] = raw
        methods[name] = method
    for name in expected:
        if found.count(name) != 1:
            errors.append({"kind": "method-count", "method": name, "count": found.count(name)})
    return methods, errors


def validate_capture(manifest, text, cohort, role, pin, source, host):
    errors = []

    def require(condition, kind, **detail):
        if not condition:
            errors.append({"kind": kind, **detail})

    require(manifest.get("exitCode") == 0 and manifest.get("timedOut") is False, "process-result")
    require(manifest.get("nativeCommit") == pin == host["nativeCommit"], "native-pin")
    require(manifest.get("coreRoot") == host["coreRoot"], "native-host-path")
    require(manifest.get("expectedMethods") == cohort["expectedMethods"], "selected-methods")
    require(manifest.get("nativeFallbackExpected") is False, "fallback")
    try:
        timestamp = datetime.fromisoformat(manifest.get("timestamp", ""))
    except (TypeError, ValueError):
        errors.append({"kind": "capture-timestamp"})
    else:
        require(timestamp.tzinfo is not None, "capture-timestamp")
    require(bool(re.fullmatch(r"[0-9a-fA-F]{64}", manifest.get("runnerHash", ""))), "runner-hash")
    environment = manifest.get("environment", {})
    selector = cohort["typeName"] + ":*"
    for key, value in {
        "DOTNET_ReadyToRun": "0", "DOTNET_TieredCompilation": "0",
        "DOTNET_JitDump": selector, "DOTNET_JitRawHexCode": selector,
    }.items():
        require(environment.get(key) == value, "capture-setting", setting=key,
                expected=value, actual=environment.get(key))
    require(environment.get("DOTNET_JitLateDisasm") in (None, "0"), "late-disassembly")
    require(environment.get("DOTNET_JitMinOpts") in (None, "0"), "minopts")
    for marker in REJECTIONS:
        require(marker not in text, "compilation-rejection", marker=marker)
    hashes = manifest.get("binaryHashes", {})
    for entry in host["files"]:
        matches = [value for path, value in hashes.items()
                   if path.replace("\\", "/").rsplit("/", 1)[-1] == entry["name"]]
        require(len(matches) == 1 and matches[0].lower() == entry["sha256"].lower(),
                "native-host-binary", file=entry["name"])
    if role == "managed":
        require(manifest.get("managedExecutionRequested") is True, "managed-execution")
        require(manifest.get("managedSource") == source, "managed-source")
        jit = manifest.get("managedJit")
        require(bool(jit) and jit in hashes, "managed-jit-binary")
        require(environment.get("DOTNET_RunAltJitCode") == "1", "managed-code-selected")
        require(environment.get("DOTNET_AltJit") == selector, "managed-selector")
        require(environment.get("DOTNET_AltJitPath") == jit, "managed-jit-path")
    else:
        require(not manifest.get("managedJit") and manifest.get("managedExecutionRequested") is False,
                "native-control")
        require(not (ALT_SETTINGS & environment.keys()), "native-altjit")
    expansions = len(re.findall(cohort["expansionPattern"], text.replace("\r\n", "\n"), re.M))
    require(expansions == cohort["expectedFulloptsExpansions"], "helper-expansion-count",
            expected=cohort["expectedFulloptsExpansions"], actual=expansions)
    return errors


def compare_pair(left, right, names, label):
    differences = []
    lm, rm = left["manifest"], right["manifest"]
    if left["stdout"] != right["stdout"]:
        differences.append({"pair": label, "kind": "runtime-output",
                            "left": left["stdout"], "right": right["stdout"]})
    for field in ("coreRoot", "corpus", "expectedMethods", "runnerHash"):
        if lm.get(field) != rm.get(field):
            differences.append({"pair": label, "kind": field,
                                "left": lm.get(field), "right": rm.get(field)})
    inputs = dict(rm.get("binaryHashes", {}))
    inputs.pop(rm.get("managedJit"), None)
    if lm.get("binaryHashes") != inputs:
        differences.append({"pair": label, "kind": "binary-inputs",
                            "left": lm.get("binaryHashes"), "right": inputs})
    environments = [
        {key: value for key, value in m.get("environment", {}).items()
         if key not in ALT_SETTINGS and key != "DOTNET_JitStdOutFile"}
        for m in (lm, rm)
    ]
    if environments[0] != environments[1]:
        differences.append({"pair": label, "kind": "environment",
                            "left": environments[0], "right": environments[1]})
    if list(left["methods"]) != list(right["methods"]):
        differences.append({"pair": label, "kind": "method-order",
                            "left": list(left["methods"]), "right": list(right["methods"])})
    for name in names:
        a, b = left["methods"].get(name), right["methods"].get(name)
        if a is None or b is None:
            differences.append({"pair": label, "method": name, "kind": "missing-method"})
            continue
        for field in ("identity", "hash"):
            if a[field] != b[field]:
                differences.append({"pair": label, "method": name, "kind": field,
                                    "left": a[field], "right": b[field]})
        if a["phase"] is not None and b["phase"] is not None and a["phase"] != b["phase"]:
            diff = "".join(difflib.unified_diff(
                a["phase"].splitlines(keepends=True), b["phase"].splitlines(keepends=True),
                fromfile=label + "-left", tofile=label + "-right",
            ))
            differences.append({"pair": label, "method": name, "kind": "full-phase", "diff": diff})
        if a["bytes"] is not None and b["bytes"] is not None and a["bytes"] != b["bytes"]:
            offsets = []
            for i in range(max(len(a["bytes"]), len(b["bytes"]))):
                av = a["bytes"][i] if i < len(a["bytes"]) else None
                bv = b["bytes"][i] if i < len(b["bytes"]) else None
                if av != bv:
                    offsets.append({"offset": i, "left": av, "right": bv})
            differences.append({"pair": label, "method": name, "kind": "raw-bytes",
                                "leftLength": len(a["bytes"]), "rightLength": len(b["bytes"]),
                                "offsets": offsets})
    return differences


def load_capture(directory, cohort, role, config, source, host):
    manifest = json.loads((directory / "manifest.json").read_text(encoding="utf-8-sig"))
    text = (directory / "jitdump.txt").read_bytes().decode("utf-8-sig")
    methods, errors = parse_dump(text, cohort)
    errors += validate_capture(manifest, text, cohort, role, config["nativeCommit"], source, host)
    stderr = (directory / "stderr.txt").read_text(encoding="utf-8-sig")
    if stderr.strip():
        errors.append({"kind": "stderr", "text": stderr})
    declared_dump = manifest.get("environment", {}).get("DOTNET_JitStdOutFile", "")
    if not declared_dump or Path(declared_dump).resolve() != (directory / "jitdump.txt").resolve():
        errors.append({"kind": "capture-path", "declared": declared_dump})
    stdout = (directory / "stdout.txt").read_bytes().decode("utf-8-sig").replace("\r\n", "\n")
    return {"manifest": manifest, "methods": methods, "errors": errors, "stdout": stdout}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--config", type=Path, required=True)
    parser.add_argument("--native-root", type=Path, required=True)
    parser.add_argument("--native-repeat-root", type=Path, required=True)
    parser.add_argument("--managed-root", type=Path, required=True)
    parser.add_argument("--managed-source", required=True)
    parser.add_argument("--native-host-receipt", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    if args.output.exists():
        parser.error("Use a fresh output file; preserve previous comparisons.")
    roots = [args.native_root, args.native_repeat_root, args.managed_root]
    if len({root.resolve() for root in roots}) != 3:
        parser.error("Native, native repeat and managed captures must be distinct directories.")
    config = json.loads(args.config.read_text(encoding="utf-8-sig"))
    host = json.loads(args.native_host_receipt.read_text(encoding="utf-8-sig"))
    if args.managed_source != config["managedCommit"]:
        parser.error("Managed source must match the configured committed-source snapshot.")
    cohorts = config["cohorts"]
    if len({c["name"] for c in cohorts}) != len(cohorts) or not cohorts:
        parser.error("Cohort names must be nonempty and unique.")
    for cohort in cohorts:
        names = cohort["expectedMethods"]
        if not names or len(set(names)) != len(names):
            parser.error("Each cohort requires nonempty, unique expected method names.")
        if not cohort["name"] or not cohort["typeName"] or not cohort["phase"]:
            parser.error("Cohort name, type and phase must be nonempty.")
        if cohort["expectedFulloptsExpansions"] <= 0 or not cohort["expansionPattern"]:
            parser.error("A positive helper-expansion count and pattern are required.")
    if sum(len(c["expectedMethods"]) for c in cohorts) != config["selectedMethodCount"]:
        parser.error("Configured selected method count does not match the cohort method lists.")
    required_host_files = {"corerun.exe", "coreclr.dll", "clrjit.dll", "System.Private.CoreLib.dll"}
    if len(host["files"]) != 4 or {entry["name"] for entry in host["files"]} != required_host_files:
        parser.error("The native-host receipt must identify the four required host binaries.")
    errors, differences = [], []
    repeat_equal = True
    managed_jit = None
    for cohort in cohorts:
        captures = []
        for root, role in zip(roots, ("native", "native-repeat", "managed")):
            directory = root / cohort["name"]
            try:
                capture = load_capture(directory, cohort, role, config, args.managed_source, host)
            except (OSError, UnicodeError, json.JSONDecodeError) as error:
                errors.append({"cohort": cohort["name"], "role": role,
                               "kind": "unreadable-capture", "detail": str(error)})
                captures.append(None)
                continue
            errors.extend({"cohort": cohort["name"], "role": role, **e} for e in capture["errors"])
            captures.append(capture)
        native, repeat, managed = captures
        if native is None or repeat is None or native["errors"] or repeat["errors"]:
            repeat_equal = False
        if native is not None and repeat is not None:
            if native["manifest"].get("timestamp") == repeat["manifest"].get("timestamp"):
                errors.append({"cohort": cohort["name"], "kind": "reused-native-control"})
                repeat_equal = False
            delta = compare_pair(native, repeat, cohort["expectedMethods"], "native-repeat")
            repeat_equal &= not delta
            differences.extend({"cohort": cohort["name"], **d} for d in delta)
        if native is not None and managed is not None:
            delta = compare_pair(native, managed, cohort["expectedMethods"], "native-managed")
            differences.extend({"cohort": cohort["name"], **d} for d in delta)
        if managed is not None:
            manifest = managed["manifest"]
            jit = (manifest.get("managedJit"),
                   manifest.get("binaryHashes", {}).get(manifest.get("managedJit")))
            if managed_jit is not None and managed_jit != jit:
                errors.append({"cohort": cohort["name"], "kind": "mixed-managed-jit"})
            managed_jit = jit
    passed = repeat_equal and not errors and not differences
    report = {
        "nativePin": config["nativeCommit"], "managedSource": args.managed_source,
        "expectedMethods": sum(len(c["expectedMethods"]) for c in cohorts),
        "nativeRepeatExact": repeat_equal, "selectedPhaseAndCodeBytesEqual": passed,
        "normalization": "CRLF to LF only; no identity, diagnostic, address or relocation masking",
        "limits": "Selected full named helper phases and complete method bytes only. Raw-hex hot-only "
                  "coverage is rejected. Referenced data/relocation targets and whole-pipeline parity "
                  "are not established. Native variation requires parent contract review.",
        "errors": errors, "differences": differences,
    }
    with args.output.open("x", encoding="utf-8") as output:
        json.dump(report, output, indent=2)
        output.write("\n")
    print(f"methods={report['expectedMethods']} errors={len(errors)} differences={len(differences)} "
          f"nativeRepeatExact={repeat_equal} selectedParity={passed}")
    return 0 if passed else 1


if __name__ == "__main__":
    sys.exit(main())
