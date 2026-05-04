# Rescue and Modernization Plan for iMLP and DPPOP

Status: implemented for the BioFSharp.ML DPPOP rescue/containerization scope.

Implemented artifacts in this repository:

- `scripts/Export-ImlpLegacyRuntime.ps1` exports the CNTK/OpenMPI runtime from `csbdocker/imlp:1.0.0`.
- `artifacts/legacy-runtime/imlp-1.0.0/legacy-runtime.tar.gz` has been rescued on this machine.
- `artifacts/legacy-runtime/imlp-1.0.0/runtime-manifest.json` records the image digest, extracted paths, and runtime environment variables.
- `artifacts/legacy-runtime/imlp-1.0.0/SHA256SUMS` records checksums for the rescued payload.
- `src/DPPOP.CLI` provides the compiled DPPOP command-line application.
- `Dockerfile` builds the DPPOP container using the rescued legacy runtime archive.
- `tests/DPPOP.Tests` covers DPPOP CLI/input compatibility behavior.
- `AGENTS.md` documents the repository workflow and legacy runtime procedure for future agents.

## Ground truth

The following points are based on inspection of the current repositories and the local Docker image state.

- `iMLP` is a `net5.0` packaged tool that still loads CNTK directly via `CNTK.CPUOnly 2.7`.
- `iMLP` already contains both CNTK and ONNX model assets in source control.
- The current `iMLP` Docker build relies on downloading CNTK from a URL that is no longer dependable.
- The local Docker image `csbdocker/imlp:1.0.0` is available and can serve as a known-good rescue source.
- The image contains working CNTK and OpenMPI runtime trees under `/usr/local/cntk` and `/usr/local/mpi`.
- The actual CNTK dependency path in the image is `/usr/local/cntk/cntk/dependencies/lib`.
- `BioFSharp.ML` is currently a library, not a packaged CLI tool.
- DPPOP prediction logic already exists in `src/BioFSharp.ML/DPPOP.fs`.
- The current DPPOP entrypoint is script-based via `src/BioFSharp.ML/Scripts/DPPOP.fsx`.
- `BioFSharp.ML` already pins SDK `8.0.100` in `global.json`.
- `BioFSharp.ML` still references `CNTK.CPUOnly 2.8.0-rc0.dev20200201`.

## Goals

Implementation status:

- Goal 1 is implemented for the local rescue artifact.
- Goal 3 is implemented for DPPOP CLI/containerization.
- Goals 2, 4, and 5 remain cross-repository or optional follow-up work for iMLP/ONNX.

1. Rescue the legacy CNTK runtime so both projects remain buildable and deployable.
2. Keep `iMLP` operational while moving it to a more modern .NET toolchain.
3. Turn DPPOP into a proper CLI and containerize it in the same operational style as `iMLP`.
4. Optionally evaluate ONNX export for DPPOP.
5. Optionally migrate both tools from CNTK inference to ONNX Runtime once parity is proven.

## Phase 1: Rescue the CNTK runtime

The first priority is to preserve a reproducible CNTK runtime independent of external URLs.

### Deliverables

- A rescued runtime artifact extracted from `csbdocker/imlp:1.0.0`.
- Checksums and minimal documentation for the rescued payload.
- A repeatable extraction script or procedure.

### Scope of rescue

Preserve the working runtime directories from the local Docker image:

- `/usr/local/cntk/cntk/lib`
- `/usr/local/cntk/cntk/dependencies/lib`
- `/usr/local/mpi/lib`

At minimum, preserve:

- CNTK managed and native libraries
- CNTK dependency libraries
- OpenMPI runtime libraries
- Any path or loader assumptions required at runtime

### Recommended storage strategy

Do not rely on rebuilding the runtime from upstream downloads.

Instead:

- archive the rescued binaries as an internal release artifact, package archive, or immutable binary backup
- record the image digest and extraction source
- store SHA256 checksums for each published archive
- include a short `README` describing required environment variables and expected directory layout

### Notes

- The local image is the strongest current recovery anchor.
- The current Dockerfile should be treated as historical documentation, not as the sole source of truth.

## Phase 2: Stabilize and modernize iMLP while staying on CNTK

`iMLP` should first be made reproducible from source without changing the inference backend.

### Objectives

- stop depending on the dead CNTK download URL
- keep existing model behavior unchanged
- move off the .NET 5-era build environment

### Proposed steps

1. Replace CNTK download logic in the Docker build with the rescued runtime artifact.
2. Correct the CNTK dependency path assumptions so the container reflects the actual working layout.
3. Build `iMLP` from source inside Docker instead of depending primarily on `dotnet tool install imlp --version ...`.
4. Add `global.json` to pin the SDK used for `iMLP`.
5. Retarget `iMLP` from `net5.0` to `net8.0` first.
6. Keep CNTK model loading unchanged during this phase.
7. Add a smoke test that runs `imlp` on a known short sequence and checks for non-empty output.

### Why `net8.0` first

- `BioFSharp.ML` is already aligned to .NET 8 SDK tooling.
- It reduces the number of moving parts compared to jumping directly to a newer target.
- It gives a supported LTS baseline before any runtime migration work.

### Acceptance criteria

- `iMLP` builds from source in a container without external CNTK downloads.
- The rescued CNTK runtime is sufficient for prediction execution.
- The CLI output for a small golden input remains unchanged or acceptably close.

## Phase 3: Containerize DPPOP as a real CLI

DPPOP should become a proper executable rather than remaining script-driven.

### Current state

- core prediction logic already exists in the library
- the repo does not currently expose DPPOP as a tool project
- the current script is useful as a reference, but not as the deployment artifact

### Proposed implementation

Create a new executable project, for example:

- `src/DPPOP.CLI`

This project should:

- reference `BioFSharp.ML`
- expose a stable command-line interface
- reuse `DPPOP.Prediction.scoreDppopPlant`
- reuse `DPPOP.Prediction.scoreDppopNonPlant`
- optionally support a custom model path

### Suggested CLI shape

- `--proteome <fasta>`
- `--proteins-of-interest <fasta>`
- `--model plant|nonplant|custom`
- `--custom-model <path>` when applicable
- `--output <tsv>`

### Suggested output schema

One peptide per row, with at least:

- `ProteinId`
- `Sequence`
- `PredictionScore`
- `Distinct`

### Containerization approach

Containerize DPPOP in the same operational pattern as `iMLP`:

- same rescued CNTK/OpenMPI runtime base
- same `/data` working directory convention
- same bind-mount based workflow
- same expectation that FASTA inputs are mounted from host paths

### Acceptance criteria

- DPPOP can be run non-interactively from a container
- the CLI accepts FASTA inputs and writes deterministic TSV output
- the container no longer depends on ad hoc F# script execution

## Phase 4: Unify runtime and image layout

Once both tools run on the rescued CNTK stack, consolidate the container setup.

### Recommended structure

Build a shared base image or common Docker stage that contains:

- rescued CNTK runtime
- rescued OpenMPI runtime
- required environment variables

Then build:

- an `iMLP` image on top of that base
- a `DPPOP` image on top of that base

### Benefits

- one maintained legacy runtime layer
- less duplication
- easier recovery if future registry URLs disappear
- clearer operational documentation

## Optional Phase 5: Export DPPOP models to ONNX

This is worth exploring, but should be treated as a separate validation track rather than part of the rescue path.

### Why this is optional

- the core rescue problem is runtime preservation, not model conversion
- model export may fail or produce numerically different results
- it should not block containerization

### Proposed work

1. Identify the exact CNTK graph characteristics of the DPPOP models.
2. Attempt export of the plant and non-plant models to ONNX.
3. Build a golden test set of peptide predictions.
4. Compare CNTK vs ONNX outputs for:
   - raw scores
   - sorted peptide ranking
   - normalized relative output

### Acceptance criteria

- ONNX export succeeds for both models
- output parity is within a pre-defined tolerance
- no feature preprocessing mismatch is introduced

## Optional Phase 6: Move both projects to ONNX Runtime

This phase should only begin after ONNX parity is proven.

### iMLP

`iMLP` is the easier candidate because ONNX model assets already exist in the repository.

Required work will likely include:

- wiring ONNX Runtime into the existing prediction path
- handling model input shape and output shape explicitly
- verifying whether sidecar `Parameter*` files are required at runtime
- validating predictions against the current CNTK implementation

### DPPOP

DPPOP may be migratable after successful export, but this depends on the exact CNTK graph and preprocessing assumptions.

Required work will likely include:

- ONNX export validation
- a new inference implementation using ONNX Runtime
- parity testing against existing CNTK results

### Acceptance criteria

- CNTK and ONNX outputs match within accepted tolerance
- the modern runtime path is production-ready
- CNTK can remain as a fallback until confidence is high enough to remove it

## Recommended order of execution

1. Rescue and archive the working CNTK/OpenMPI runtime from `csbdocker/imlp:1.0.0`.
2. Make `iMLP` build from source in Docker using the rescued runtime.
3. Retarget `iMLP` to a modern .NET toolchain while keeping CNTK.
4. Add a proper `DPPOP.CLI` executable project.
5. Containerize DPPOP on the same rescued runtime base.
6. Consolidate both tools onto a shared legacy-runtime image layout.
7. Explore DPPOP ONNX export.
8. Only then evaluate full ONNX Runtime migration for one or both projects.

## Risks and constraints

- CNTK is deprecated and should be treated as a legacy dependency to preserve, not extend further than necessary.
- Native dependency loading is the most fragile part of both toolchains.
- Upgrading framework targets and switching inference runtimes in the same change would add unnecessary risk.
- DPPOP currently lacks meaningful automated tests around real prediction behavior.
- Existing Docker metadata and source may not perfectly reflect the live working runtime; the local image is therefore especially valuable.

## Practical next tasks

The next implementation backlog should start with the following concrete tasks:

1. Add a binary rescue procedure for CNTK/OpenMPI based on the local `csbdocker/imlp:1.0.0` image.
2. Patch the `iMLP` Docker build to use the rescued runtime artifact.
3. Add `global.json` and retarget `iMLP` to `net8.0`.
4. Add a minimal container smoke test for `iMLP`.
5. Scaffold `src/DPPOP.CLI` in `BioFSharp.ML`.
6. Define DPPOP CLI arguments and TSV output format.
7. Containerize DPPOP using the same runtime base as `iMLP`.
8. Add golden-result tests for both tools before attempting ONNX migration.

## Assumptions

- The local Docker image `csbdocker/imlp:1.0.0` remains available long enough to extract the rescue artifacts.
- CNTK package restore may still work in some environments, but should not be trusted as the primary recovery mechanism.
- ONNX migration is desirable, but not required to complete the immediate rescue and containerization effort.
