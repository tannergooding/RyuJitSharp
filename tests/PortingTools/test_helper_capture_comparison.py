import importlib.util
import json
import shutil
import subprocess
import sys
import unittest
from pathlib import Path


sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[2]
SCRIPT = ROOT / "scripts" / "porting" / "compare_helper_captures.py"
spec = importlib.util.spec_from_file_location("helper_compare", SCRIPT)
compare = importlib.util.module_from_spec(spec)
spec.loader.exec_module(compare)
PIN = "3" * 40
SOURCE = "6" * 40
COHORT = {
    "name": "sample", "typeName": "Cases", "expectedMethods": ["First", "Second"],
    "phase": "Expand TLS access", "expansionPattern": "^Expanded$",
    "expectedFulloptsExpansions": 2,
}
HOST = {
    "nativeCommit": PIN, "coreRoot": r"D:\Core_Root",
    "files": [{"name": name, "sha256": "a" * 64} for name in
              ("corerun.exe", "coreclr.dll", "clrjit.dll", "System.Private.CoreLib.dll")],
}


def dump(name="First", raw="0102", tail="profile check\n", size=2):
    return (
        f"****** START compiling Cases:{name}():int (MethodHash=12345678)\n"
        "*************** Starting PHASE Expand TLS access\n"
        "Expanded\n"
        "*************** Finishing PHASE Expand TLS access\n"
        f"Trees after Expand TLS access\n{tail}"
        "*************** Starting PHASE Next phase\n"
        "*************** Finishing PHASE Next phase\n"
        f"Generated native code for Cases:{name}():int:\n{raw}\n\n"
        f"Method code size: {size}\n"
    )


def manifest(managed=False):
    selector = "Cases:*"
    hashes = {r"D:\Core_Root" + "\\" + f["name"]: f["sha256"] for f in HOST["files"]}
    hashes[r"D:\corpus.dll"] = "b" * 64
    environment = {
        "DOTNET_ReadyToRun": "0", "DOTNET_TieredCompilation": "0",
        "DOTNET_JitDump": selector, "DOTNET_JitRawHexCode": selector,
        "DOTNET_JitStdOutFile": "different per capture",
    }
    jit = r"D:\fresh-jit.dll" if managed else ""
    if managed:
        hashes[jit] = "c" * 64
        environment.update({
            "DOTNET_AltJit": selector, "DOTNET_AltJitName": "fresh-jit.dll",
            "DOTNET_AltJitPath": jit, "DOTNET_RunAltJitCode": "1",
        })
    return {
        "timestamp": "2026-09-30T18:44:37.056-07:00", "runnerHash": "f" * 64,
        "nativeCommit": PIN, "coreRoot": HOST["coreRoot"], "exitCode": 0, "timedOut": False,
        "expectedMethods": COHORT["expectedMethods"], "nativeFallbackExpected": False,
        "corpus": r"D:\corpus.dll", "environment": environment, "binaryHashes": hashes,
        "managedExecutionRequested": managed, "managedSource": SOURCE if managed else "",
        "managedJit": jit,
    }


def capture(text=None, managed=False):
    text = text if text is not None else dump() + dump("Second")
    methods, errors = compare.parse_dump(text, COHORT)
    return {"manifest": manifest(managed), "methods": methods, "errors": errors, "stdout": "ok\n"}


class HelperCaptureComparisonTests(unittest.TestCase):
    def test_complete_phase_includes_post_phase_diagnostics(self):
        methods, errors = compare.parse_dump(dump() + dump("Second"), COHORT)
        self.assertEqual(errors, [])
        self.assertIn("profile check\n", methods["First"]["phase"])
        self.assertNotIn("Starting PHASE Next phase", methods["First"]["phase"])

    def test_crlf_only_is_equivalent(self):
        a = capture()
        b = capture((dump() + dump("Second")).replace("\n", "\r\n"), managed=True)
        self.assertEqual(compare.compare_pair(a, b, COHORT["expectedMethods"], "pair"), [])

    def test_bare_carriage_return_is_not_normalized(self):
        a = capture()
        b = capture(dump(tail="profile\rcheck\n") + dump("Second"))
        self.assertEqual(b["errors"], [])
        differences = compare.compare_pair(a, b, COHORT["expectedMethods"], "pair")
        self.assertEqual([d["kind"] for d in differences], ["full-phase"])

    def test_post_phase_differences_are_not_discarded(self):
        a = capture()
        b = capture(dump(tail="different VN\n") + dump("Second", tail="other profile\n"))
        differences = compare.compare_pair(a, b, COHORT["expectedMethods"], "pair")
        self.assertEqual([d["kind"] for d in differences], ["full-phase", "full-phase"])
        self.assertIn("different VN", differences[0]["diff"])

    def test_all_raw_byte_offsets_are_reported(self):
        a = capture()
        b = capture(dump(raw="FFEE") + dump("Second", raw="0103"))
        differences = compare.compare_pair(a, b, COHORT["expectedMethods"], "pair")
        self.assertEqual([d["offset"] for d in differences[0]["offsets"]], [0, 1])
        self.assertEqual(differences[1]["offsets"], [{"offset": 1, "left": 2, "right": 3}])

    def test_hot_only_code_is_rejected(self):
        _, errors = compare.parse_dump(dump(size=3) + dump("Second"), COHORT)
        self.assertIn("raw-code-coverage", [e["kind"] for e in errors])

    def test_missing_duplicate_unexpected_and_wrong_raw_identity(self):
        text = dump() + dump() + dump("Unexpected") + dump("Second").replace(
            "Generated native code for Cases:Second()", "Generated native code for Cases:Other()"
        )
        _, errors = compare.parse_dump(text, COHORT)
        kinds = {e["kind"] for e in errors}
        self.assertTrue({"duplicate-method", "unexpected-method", "raw-code-identity", "method-count"} <= kinds)

    def test_missing_phase_finish_and_invalid_hex_are_rejected(self):
        text = (dump(raw="ABC") + dump("Second")).replace(
            "*************** Finishing PHASE Expand TLS access", "missing finish"
        )
        _, errors = compare.parse_dump(text, COHORT)
        self.assertTrue({"phase-finish", "raw-code-format"} <= {e["kind"] for e in errors})

    def test_fallback_wrong_pin_host_binary_and_source_are_rejected(self):
        m = manifest(True)
        m["nativeFallbackExpected"] = True
        m["nativeCommit"] = "0" * 40
        m["managedSource"] = "old"
        m["binaryHashes"][r"D:\Core_Root\clrjit.dll"] = "d" * 64
        errors = compare.validate_capture(m, dump() + dump("Second"), COHORT, "managed", PIN, SOURCE, HOST)
        self.assertTrue({"fallback", "native-pin", "managed-source", "native-host-binary"} <=
                        {e["kind"] for e in errors})

    def test_native_altjit_and_disabled_raw_capture_are_rejected(self):
        m = manifest(True)
        m["environment"].pop("DOTNET_JitRawHexCode")
        errors = compare.validate_capture(m, dump() + dump("Second"), COHORT, "native", PIN, SOURCE, HOST)
        self.assertTrue({"native-control", "native-altjit", "capture-setting"} <= {e["kind"] for e in errors})

    def test_compilation_rejections_and_no_positive_helper_work_are_rejected(self):
        text = (dump() + dump("Second")).replace("Expanded", "Nothing to expand") + "CORJIT_SKIPPED"
        errors = compare.validate_capture(manifest(), text, COHORT, "native", PIN, SOURCE, HOST)
        self.assertTrue({"compilation-rejection", "helper-expansion-count"} <= {e["kind"] for e in errors})

    def test_input_and_runtime_output_differences_are_reported(self):
        a, b = capture(), capture()
        b["manifest"]["binaryHashes"][r"D:\corpus.dll"] = "e" * 64
        b["stdout"] = "wrong\n"
        kinds = {d["kind"] for d in compare.compare_pair(a, b, COHORT["expectedMethods"], "pair")}
        self.assertTrue({"binary-inputs", "runtime-output"} <= kinds)

    def test_invalid_timestamp_and_missing_runner_binding_are_rejected(self):
        m = manifest()
        m["timestamp"] = ""
        m.pop("runnerHash")
        errors = compare.validate_capture(m, dump() + dump("Second"), COHORT, "native", PIN, SOURCE, HOST)
        self.assertTrue({"capture-timestamp", "runner-hash"} <= {e["kind"] for e in errors})

    def test_capture_runner_hides_child_before_start(self):
        text = (ROOT / "scripts" / "porting" / "Invoke-PortingCorpus.ps1").read_text()
        self.assertLess(text.index("$start.CreateNoWindow = $true"), text.index("$process.Start()"))
        self.assertIn("$start.UseShellExecute = $false", text)


class HelperCaptureCommandTests(unittest.TestCase):
    def setUp(self):
        self.root = ROOT / "artifacts" / "helper-comparer-script-input-tests"
        self.root.mkdir(parents=True, exist_ok=False)
        self.addCleanup(shutil.rmtree, self.root)
        self.config = self.root / "config.json"
        self.config.write_text(json.dumps({
            "nativeCommit": PIN, "managedCommit": SOURCE, "cohorts": [COHORT], "selectedMethodCount": 2,
        }))
        (self.root / "host.json").write_text(json.dumps(HOST))
        for role in ("native", "native-repeat", "managed"):
            folder = self.root / role / "sample"
            folder.mkdir(parents=True)
            m = manifest(role == "managed")
            second = {"native": 37, "native-repeat": 38, "managed": 39}[role]
            m["timestamp"] = f"2026-09-30T18:44:{second}.056-07:00"
            m["environment"]["DOTNET_JitStdOutFile"] = str(folder / "jitdump.txt")
            (folder / "manifest.json").write_text(json.dumps(m))
            (folder / "jitdump.txt").write_text(dump() + dump("Second"))
            (folder / "stdout.txt").write_text("ok\n")
            (folder / "stderr.txt").write_text("")

    def run_compare(self):
        args = [
            sys.executable, "-B", str(SCRIPT), "--config", str(self.config),
            "--native-root", str(self.root / "native"),
            "--native-repeat-root", str(self.root / "native-repeat"),
            "--managed-root", str(self.root / "managed"),
            "--managed-source", SOURCE, "--native-host-receipt", str(self.root / "host.json"),
            "--output", str(self.root / "comparison.json"),
        ]
        result = subprocess.run(
            args, capture_output=True, text=True, check=False,
            creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0),
        )
        output = self.root / "comparison.json"
        return result.returncode, json.loads(output.read_text()) if output.exists() else None

    def test_complete_synthetic_inputs_pass(self):
        code, report = self.run_compare()
        self.assertEqual(code, 0)
        self.assertTrue(report["nativeRepeatExact"])
        self.assertTrue(report["selectedPhaseAndCodeBytesEqual"])

    def test_source_not_bound_to_configured_commit_is_rejected(self):
        config = json.loads(self.config.read_text())
        config["managedCommit"] = "7" * 40
        self.config.write_text(json.dumps(config))
        code, report = self.run_compare()
        self.assertEqual(code, 2)
        self.assertIsNone(report)

    def test_native_repeat_variation_blocks_parity_and_reports_all_methods(self):
        path = self.root / "native-repeat" / "sample" / "jitdump.txt"
        path.write_text(dump(raw="AABB") + dump("Second", raw="0203"))
        code, report = self.run_compare()
        self.assertEqual(code, 1)
        self.assertFalse(report["nativeRepeatExact"])
        self.assertFalse(report["selectedPhaseAndCodeBytesEqual"])
        self.assertEqual(len(report["differences"]), 2)

    def test_missing_repeat_capture_is_failure_not_a_skipped_control(self):
        (self.root / "native-repeat" / "sample" / "jitdump.txt").unlink()
        code, report = self.run_compare()
        self.assertEqual(code, 1)
        self.assertFalse(report["nativeRepeatExact"])
        self.assertEqual(report["errors"][0]["kind"], "unreadable-capture")

    def test_copied_native_control_is_rejected(self):
        native_path = self.root / "native" / "sample" / "manifest.json"
        repeat_path = self.root / "native-repeat" / "sample" / "manifest.json"
        native = json.loads(native_path.read_text())
        repeated = json.loads(repeat_path.read_text())
        repeated["timestamp"] = native["timestamp"]
        repeat_path.write_text(json.dumps(repeated))
        code, report = self.run_compare()
        self.assertEqual(code, 1)
        self.assertFalse(report["nativeRepeatExact"])
        self.assertIn("reused-native-control", [e["kind"] for e in report["errors"]])

    def test_copied_output_path_is_rejected(self):
        repeat_path = self.root / "native-repeat" / "sample" / "manifest.json"
        repeated = json.loads(repeat_path.read_text())
        repeated["environment"]["DOTNET_JitStdOutFile"] = str(
            self.root / "native" / "sample" / "jitdump.txt"
        )
        repeat_path.write_text(json.dumps(repeated))
        code, report = self.run_compare()
        self.assertEqual(code, 1)
        self.assertFalse(report["nativeRepeatExact"])
        self.assertIn("capture-path", [e["kind"] for e in report["errors"]])


if __name__ == "__main__":
    unittest.main()
