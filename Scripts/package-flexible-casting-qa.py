#!/usr/bin/env python3
"""Build a closed diagnostic QA variant from clean committed feature sources; no release upload."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import zipfile

ROOT = Path(__file__).resolve().parents[1]
PRIVATE_DLLS = {
    "Mono.Cecil.dll", "Mono.Cecil.Mdb.dll", "Mono.Cecil.Pdb.dll", "Mono.Cecil.Rocks.dll",
    "MonoMod.RuntimeDetour.dll", "MonoMod.Utils.dll", "NAudio.dll", "Tomlet.dll",
    "UnityExplorer.STANDALONE.Mono.dll", "UniverseLib.Mono.dll", "mcs.dll",
}
BUILD_ID = "13.7C.2-EDITOR-ONLY"


def git(*args):
    return subprocess.check_output(["git", *args], cwd=ROOT, text=True).strip()


def sha256(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--out", type=Path, default=ROOT.parent / "artifacts" / "flexible-casting-qa")
    args = parser.parse_args()
    out = args.out.resolve()
    if out == ROOT or ROOT in out.parents:
        raise SystemExit("Output must be outside the Git checkout.")
    if git("branch", "--show-current") != "feature/progression-20-to-12":
        raise SystemExit("Only feature/progression-20-to-12 is authorized.")
    if git("diff", "HEAD", "--name-only"):
        raise SystemExit("Commit/review tracked changes first; package identity must describe committed sources.")
    # Existing untracked native build output is ignored, never copied as a package source.
    unknown = git("ls-files", "--others", "--exclude-standard").splitlines()
    if any(not p.startswith("SolastaFiles/Mods/") for p in unknown):
        raise SystemExit("Unexpected untracked source/artifact: review before packaging.")
    commit = git("rev-parse", "HEAD")
    env = os.environ.copy()
    dotnet = shutil.which("dotnet") or "/workspace/.dotnet/dotnet"
    if not Path(dotnet).is_file():
        raise SystemExit(".NET SDK9.0.320 is required; use the documented prepared environment.")
    if subprocess.check_output([dotnet, "--version"], cwd=ROOT, env=env, text=True).strip() != "9.0.320":
        raise SystemExit("The active SDK must be exactly9.0.320.")
    if not env.get("SolastaInstallDir"):
        raise SystemExit("Set SolastaInstallDir to the prepared game-reference directory first.")
    out.mkdir(parents=True, exist_ok=True)
    stem = "SolastaUnfinishedBusiness-QA-" + BUILD_ID + "-" + commit[:12]
    archive = out / (stem + ".zip")
    if archive.exists():
        raise SystemExit("Artifact already exists; choose a fresh --out directory.")
    with tempfile.TemporaryDirectory(prefix="solasta-qa-") as temp:
        temp = Path(temp)
        binary = temp / "build"
        build = [dotnet, "build", "SolastaUnfinishedBusiness.sln", "--no-restore", "-c", "Release Workflow", "-m:2",
                 "-p:SolastaFlexibleCastingQa=true", "-p:QaSourceCommit=" + commit, "-p:OutputPath=" + str(binary) + os.sep,
                 "-warnaserror"]
        with (out / (stem + "-build.txt")).open("w", encoding="utf-8") as log:
            subprocess.run(build, cwd=ROOT, env=env, stdout=log, stderr=subprocess.STDOUT, check=True)
        audit = [dotnet, "run", "--project", "Tests/FighterProgression/FighterProgression.Tests.csproj", "--",
                 "--qa-audit", str(binary / "SolastaUnfinishedBusiness.dll"), commit]
        with (out / (stem + "-audit.txt")).open("w", encoding="utf-8") as log:
            subprocess.run(audit, cwd=ROOT, env=env, stdout=log, stderr=subprocess.STDOUT, check=True)
        mod = temp / "package" / "SolastaUnfinishedBusiness"
        mod.mkdir(parents=True)
        for name in sorted(PRIVATE_DLLS | {"SolastaUnfinishedBusiness.dll"}):
            source = binary / name
            if not source.is_file():
                raise SystemExit("Expected private runtime dependency is absent: " + name)
            shutil.copy2(source, mod / name)
        for folder in ("BlenderModels", "Portraits", "UnofficialTranslations"):
            if (binary / folder).is_dir():
                shutil.copytree(binary / folder, mod / folder)
        info = json.loads((ROOT / "SolastaUnfinishedBusiness" / "Info.json").read_text(encoding="utf-8-sig"))
        info["DisplayName"] = "UB QA " + BUILD_ID + " [" + commit[:12] + "]"
        info["VersionURL"] = ""  # QA native updater also compiled out; no release/update route.
        info["Repository"] = "https://github.com/rork2025-beep/SolastaUnfinishedBusiness"
        (mod / "Info.json").write_text(json.dumps(info, indent=2) + "\n", encoding="utf-8")
        for doc in ("FlexibleCastingQaSmokeChecklist.ru.md", "FlexibleCastingQaBuild.md"):
            shutil.copy2(ROOT / "Documentation" / doc, mod / doc)
        manifest = {"build_id": BUILD_ID, "source_commit": commit, "admitted": False, "mixed_gameplay_ready": False,
                    "status": "C.2 EDITOR ONLY; campaign admission/XP/conversion smoke blocked until C.3",
                    "files": {str(p.relative_to(mod)).replace(os.sep, "/"): sha256(p) for p in sorted(mod.rglob("*")) if p.is_file()}}
        (mod / "QA-MANIFEST.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
        with zipfile.ZipFile(archive, "w", compression=zipfile.ZIP_DEFLATED, compresslevel=6) as z:
            for file in sorted(mod.rglob("*")):
                if file.is_file():
                    z.write(file, str(file.relative_to(mod.parent)))
        with zipfile.ZipFile(archive) as z:
            if z.testzip() is not None:
                raise SystemExit("ZIP integrity check failed.")
            if any("/Settings/" in n or "Assembly-CSharp" in n or n.endswith("Settings.xml") for n in z.namelist()):
                raise SystemExit("Package contains forbidden settings or native assembly.")
    checksum = sha256(archive)
    (out / (stem + ".sha256")).write_text(checksum + "  " + archive.name + "\n", encoding="utf-8")
    print(json.dumps({"artifact": str(archive), "source_commit": commit, "sha256": checksum, "bytes": archive.stat().st_size,
                      "status": "EDITOR ONLY, NOT a campaign conversion smoke-ready build"}, indent=2))


if __name__ == "__main__":
    main()
