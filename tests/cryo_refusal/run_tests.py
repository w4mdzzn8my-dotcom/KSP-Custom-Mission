"""Compile and run the actual cryo refusal probe against narrow recording KSP fakes."""
import argparse
import json
import pathlib
import shutil
import subprocess
import tempfile

parser = argparse.ArgumentParser()
parser.add_argument("--runtime", type=pathlib.Path, required=True, help="shared Microsoft.NETCore.App/VERSION directory")
parser.add_argument("--csc", type=pathlib.Path, required=True, help="Roslyn bincore/csc.dll")
parser.add_argument("--fixture-root", type=pathlib.Path, required=True, help="pristine disposable KSP-Hermes-QA root containing the three pinned inputs")
parser.add_argument("--managed", type=pathlib.Path, required=True, help="KSP 1.12.5 Managed directory (for the plugin's mscorlib reference)")
args = parser.parse_args()

repo = pathlib.Path(__file__).resolve().parents[2]
test_dir = pathlib.Path(__file__).resolve().parent
probe_source = repo / "tests" / "ksp" / "CryoRefusalProbe.cs"
with tempfile.TemporaryDirectory(prefix="cryo-refusal-probe-tests-") as tmp_name:
    tmp = pathlib.Path(tmp_name)
    fixture = tmp / "KSP-Hermes-QA"
    plugin_dir = fixture / "GameData" / "MegaCrew" / "Plugins"
    plugin_dir.mkdir(parents=True)
    for relative in (
        pathlib.Path("saves/MegaCrew Background QA/persistent.sfs"),
        pathlib.Path("GameData/MegaCrew/Plugins/MegaCrew.dll"),
        pathlib.Path("GameData/REPOSoftTech/BackgroundResources/BackgroundResources.dll"),
    ):
        source = args.fixture_root / relative
        if not source.is_file():
            raise SystemExit(f"missing fixture input: {source}")
        target = fixture / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(source, target)

    unity = tmp / "UnityEngine.CoreModule.dll"
    ksp = tmp / "Assembly-CSharp.dll"
    deepfreeze = tmp / "DeepFreezeBoundaryFakes.dll"
    probe = plugin_dir / "CryoRefusalProbe.dll"
    test_runner = tmp / "CryoRefusalProbeTests.dll"

    framework_refs = ["mscorlib.dll", "System.dll", "System.Core.dll", "System.Security.dll"]
    common = [f'-reference:"{args.managed.resolve() / name}"' for name in framework_refs]

    def compile_to(output, sources, references=()):
        rsp = tmp / (output.stem + ".rsp")
        target = "exe" if output == test_runner else "library"
        lines = ["-nologo", "-nostdlib+", "-langversion:7.3", f"-target:{target}", f'-out:"{output}"']
        lines.extend(common)
        lines.extend(f'-reference:"{reference}"' for reference in references)
        lines.extend(f'-reference:"{reference}"' for reference in (unity, ksp, deepfreeze) if reference.exists() and reference not in references)
        lines.extend(f'"{source}"' for source in sources)
        rsp.write_text("\n".join(lines) + "\n", encoding="utf-8")
        subprocess.run(["dotnet", str(args.csc), "@" + str(rsp)], check=True)

    compile_to(unity, [test_dir / "UnityBoundaryFakes.cs"])
    compile_to(ksp, [test_dir / "KspBoundaryFakes.cs"], [unity])
    compile_to(deepfreeze, [test_dir / "DeepFreezeBoundaryFakes.cs"], [unity, ksp])

    # Put fake runtime boundary assemblies beside the disposable real plugin.
    # The plugin/save/provider bytes remain the pinned inputs from fixture-root.
    shutil.copyfile(unity, plugin_dir / unity.name)
    shutil.copyfile(ksp, plugin_dir / ksp.name)
    shutil.copyfile(deepfreeze, plugin_dir / deepfreeze.name)

    compile_to(probe, [probe_source], [unity, ksp, fixture / "GameData/MegaCrew/Plugins/MegaCrew.dll"])
    compile_to(test_runner, [test_dir / "CryoRefusalProbeTests.cs"], [unity, ksp, deepfreeze])

    version = args.runtime.name
    major = version.split(".")[0]
    (tmp / "CryoRefusalProbeTests.runtimeconfig.json").write_text(json.dumps({
        "runtimeOptions": {"tfm": f"net{major}.0", "framework": {"name": "Microsoft.NETCore.App", "version": version}}
    }), encoding="utf-8")
    subprocess.run(["dotnet", str(test_runner), str(fixture), str(probe)], check=True)
