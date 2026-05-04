# Agent Instructions

## Repository Shape

This repository contains `BioFSharp.ML`, an F#/.NET library with CNTK-backed machine learning helpers, plus a DPPOP command-line wrapper.

- `BioFSharp.ML.sln` is the root solution.
- `src/BioFSharp.ML` contains the library and embedded DPPOP CNTK model resources.
- `src/DPPOP.CLI` contains the `dppop` executable/tool project.
- `tests/BioFSharp.ML.Tests` and `tests/DPPOP.Tests` contain xUnit tests.
- `build` contains the FAKE build project.
- `plans/rescue_modernize.md` records the CNTK rescue and DPPOP modernization plan.
- `scripts/Export-ImlpLegacyRuntime.ps1` extracts the legacy CNTK/OpenMPI runtime from the local Docker image.

## Build And Test

The repo pins .NET SDK `8.0.100` in `global.json` with `latestMinor` roll-forward.

Use the FAKE entry points from the repository root:

```powershell
.\build.cmd
.\build.cmd RunTests
```

On Unix-like shells:

```bash
./build.sh
./build.sh RunTests
```

The default build target builds the solution. `RunTests` cleans, builds, and runs both test projects with coverage collection enabled.

## Legacy CNTK Runtime

CNTK is a preserved legacy dependency. Treat it as runtime infrastructure to rescue and stabilize, not as a dependency to modernize casually.

The Dockerfile expects this archive to exist before building the container:

```text
artifacts/legacy-runtime/imlp-1.0.0/legacy-runtime.tar.gz
```

Create it from the known-good local image:

```powershell
.\scripts\Export-ImlpLegacyRuntime.ps1
```

The script reads `csbdocker/imlp:1.0.0` by default and writes:

- `legacy-runtime.tar.gz`
- `runtime-manifest.json`
- `SHA256SUMS`

The rescued archive contains:

- `/usr/local/cntk/cntk/lib`
- `/usr/local/cntk/cntk/dependencies/lib`
- `/usr/local/mpi/lib`

The container runtime path assumptions are:

```text
PATH=/usr/local/cntk/cntk/lib:/usr/local/mpi/bin:$PATH
LD_LIBRARY_PATH=/usr/local/cntk/cntk/dependencies/lib:/usr/local/cntk/cntk/lib:/usr/local/mpi/lib:$LD_LIBRARY_PATH
```

`artifacts/` is gitignored, so do not assume the rescued runtime is present in a fresh checkout.

## Container Workflow

After rescuing the runtime archive, build the DPPOP container from the repository root:

```powershell
docker build -t dppop .
```

Run it with input files mounted under `/data`:

```powershell
docker run --rm --mount "type=bind,source=C:/my-data,target=/data" dppop --proteome /data/proteome.fasta --proteins-of-interest /data/targets.fasta --model nonplant --output /data/results.tsv
```

## Development Notes

- Keep the library target conservative unless a task explicitly requires a target change; `src/BioFSharp.ML` currently targets `netstandard2.0`.
- `src/DPPOP.CLI` targets `net8.0` and references the library project.
- Keep DPPOP CLI behavior script-compatible where practical, but prefer the compiled CLI for deployment and tests.
- Be careful around project and solution registration when adding, renaming, or moving F# files. F# compile order is explicit in `.fsproj` files.
- Do not replace the CNTK runtime rescue path with upstream downloads. The modernization plan treats the local image as the recovery anchor.
