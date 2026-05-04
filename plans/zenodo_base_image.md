# Plan: Zenodo-anchored CNTK + .NET 10 base image, published to Docker Hub

## Context

The current [Dockerfile](Dockerfile) is monolithic: it builds DPPOP and bakes in the rescued CNTK/OpenMPI runtime in one image, copying from `artifacts/legacy-runtime/imlp-1.0.0/legacy-runtime.tar.gz` (647 MB, gitignored). Anyone outside this machine cannot reproduce the build because the tarball lives nowhere public, and re-building from a fresh checkout requires re-running [scripts/Export-ImlpLegacyRuntime.ps1](scripts/Export-ImlpLegacyRuntime.ps1) against a still-existing local `csbdocker/imlp:1.0.0` image. That defeats the rescue goal.

This plan operationalizes **Phase 4** of [plans/rescue_modernize.md](plans/rescue_modernize.md) ("Unify runtime and image layout") and adds two pieces it does not currently cover:

1. A public, citable home for the rescued binaries on **Zenodo** (DOI, versioned, immutable, distributed under a documented multi-license bundle — see Phase 0).
2. A published **`csbdocker/cntk-dotnet:dotnet10`** base image on Docker Hub that any downstream tool (DPPOP, iMLP, future) can `FROM` without owning the rescue procedure.

Outcome: `docker pull csbdocker/cntk-dotnet:1.0.1-cntk2.7-dotnet10` gives any user a working .NET 10 runtime with CNTK 2.7 + OpenMPI preloaded and `LD_LIBRARY_PATH` configured. The DPPOP image becomes a thin layer on top. Reproduction does not depend on local Docker state.

User decisions (locked):
- Base image is **runtime-only** (`mcr.microsoft.com/dotnet/runtime:10.0`).
- DPPOP.CLI **targets `net10.0`**; [src/BioFSharp.ML/BioFSharp.ML.fsproj](src/BioFSharp.ML/BioFSharp.ML.fsproj) stays on `netstandard2.0`.
- Zenodo is the canonical source and contains the base-image Dockerfile; this repo consumes the published Docker Hub base image and does not track a separate base Dockerfile.
- The rescued archive is **not** a single-licensed CNTK+OpenMPI bundle. It contains ~12 third-party native libraries with mixed licenses; redistribution requires a `NOTICE.md` enumerating each component (see Phase 0).

---

## Phase 0 — Compile third-party license inventory and NOTICE file (first implementation step)

This is the first thing to land. Phase A cannot publish to Zenodo without it, and Phase E (docs) needs it to point at. The NOTICE file is committed in-repo and uploaded as part of the Zenodo record verbatim.

### Background

Inspection of `legacy-runtime.tar.gz` (368 entries, no `LICENSE`/`COPYING`/`NOTICE` files bundled by the original CNTK distribution) shows the archive contains substantially more than CNTK and OpenMPI. The archive is a faithful copy of what Microsoft shipped with the official CNTK 2.7 Linux release; redistributing it is well-precedented, but the licenses must be enumerated honestly.

### License inventory

| Component (file pattern in archive) | Upstream | SPDX / License | Notes |
|---|---|---|---|
| `Cntk.*`, `libCntk.*` | [microsoft/CNTK](https://github.com/microsoft/CNTK) | `MIT` | The headline component. |
| `libmultiverso.so` | [microsoft/Multiverso](https://github.com/microsoft/Multiverso) | `MIT` | |
| `Microsoft.VisualStudio.TestPlatform.*.dll` | [microsoft/vstest](https://github.com/microsoft/vstest) | `MIT` | Shipped by CNTK's CI; not strictly needed at runtime, kept for byte-identical preservation. |
| `libmpi*.so*`, `libopen-pal*`, `libopen-rte*`, `libompitrace*`, `libmca_common_sm*`, `libopen-trace-format*` | [Open MPI 1.10](https://www.open-mpi.org/community/license.php) | `BSD-3-Clause` | |
| `libopenblas.so.0` | [OpenBLAS](https://github.com/OpenMathLib/OpenBLAS) | `BSD-3-Clause` | |
| `libopencv_core.so.3.1`, `libopencv_imgproc.so.3.1`, `libopencv_imgcodecs.so.3.1` | [OpenCV 3.x](https://opencv.org/license/) | `BSD-3-Clause` | OpenCV 4.5+ moved to Apache-2.0; these 3.1 binaries predate that. |
| `libkaldi-*.so` (base, hmm, lat, matrix, nnet, tree, util, cudamatrix) | [Kaldi](https://github.com/kaldi-asr/kaldi) | `Apache-2.0` | |
| `libfst.so.3` | [OpenFST](https://www.openfst.org/) | `Apache-2.0` | |
| `libmkldnn.so.0` | [oneDNN / MKL-DNN](https://github.com/oneapi-src/oneDNN) | `Apache-2.0` | |
| `libzip.so.4` | [libzip](https://libzip.org/) | `BSD-3-Clause` | |
| `libmklml_intel.so` | Intel MKL-ML (the cut-down inference build Microsoft shipped with CNTK) | **Intel Simplified Software License** | Proprietary but redistributable as a runtime; Microsoft shipped this exact file in the public CNTK 2.7 release. |
| `libiomp5.so` | Intel OpenMP runtime | **Intel Runtime Redistribution License** | Same redistribution status as MKL-ML. |

### Deliverable: `artifacts/legacy-runtime/imlp-1.0.0/NOTICE.md`

Single Markdown file that:

1. States the archive is an unmodified binary redistribution of components originally bundled in the public Microsoft CNTK 2.7 Linux release (image source: `csbdocker/imlp@sha256:c5781e2a198c7281e9fd4d46bff385c3f2c13b8eaa56538bc598f4bd249fdc34`).
2. Reproduces the table above as the canonical inventory.
3. For each component: includes either the **full upstream license text** verbatim (preferred for short licenses: MIT, BSD-3, Apache-2.0 header + reference) or a stable URL to the license. The Intel licenses must be reproduced in full because they are not standard SPDX texts and may move/disappear from Intel's site.
4. Calls out the Intel MKL-ML and `libiomp5` redistribution clauses explicitly: "These files are redistributed under Intel's runtime redistribution grant. Downstream redistributors must preserve this NOTICE."
5. Adds a "How this inventory was produced" footer pointing at [scripts/Export-ImlpLegacyRuntime.ps1](scripts/Export-ImlpLegacyRuntime.ps1) and the `tar -tzf` listing it was derived from.

### Suggested file layout

```
artifacts/legacy-runtime/imlp-1.0.0/
├── legacy-runtime.tar.gz          (existing)
├── runtime-manifest.json          (existing)
├── SHA256SUMS                     (existing)
└── NOTICE.md                      (NEW — created in this phase)
```

### Implementation tasks

1. Fetch upstream license texts (one-time, archive into the NOTICE):
   - CNTK MIT — [github.com/microsoft/CNTK/blob/master/LICENSE.md](https://github.com/microsoft/CNTK/blob/master/LICENSE.md)
   - Multiverso MIT — [github.com/microsoft/Multiverso/blob/master/LICENSE](https://github.com/microsoft/Multiverso/blob/master/LICENSE)
   - vstest MIT — [github.com/microsoft/vstest/blob/main/LICENSE](https://github.com/microsoft/vstest/blob/main/LICENSE)
   - Open MPI BSD-3 — [open-mpi.org/community/license.php](https://www.open-mpi.org/community/license.php)
   - OpenBLAS BSD-3 — [github.com/OpenMathLib/OpenBLAS/blob/develop/LICENSE](https://github.com/OpenMathLib/OpenBLAS/blob/develop/LICENSE)
   - OpenCV 3.x BSD-3 — [github.com/opencv/opencv/blob/3.4/LICENSE](https://github.com/opencv/opencv/blob/3.4/LICENSE)
   - Kaldi Apache-2.0 — [github.com/kaldi-asr/kaldi/blob/master/COPYING](https://github.com/kaldi-asr/kaldi/blob/master/COPYING)
   - OpenFST Apache-2.0 — [www.openfst.org/twiki/bin/view/FST/FstDownload](https://www.openfst.org/twiki/bin/view/FST/FstDownload)
   - oneDNN Apache-2.0 — [github.com/oneapi-src/oneDNN/blob/main/LICENSE](https://github.com/oneapi-src/oneDNN/blob/main/LICENSE)
   - libzip BSD-3-style — [libzip.org/license/](https://libzip.org/license/)
   - Intel Simplified Software License (MKL-ML) — capture the exact version that shipped with CNTK 2.7; reference: [intel.com/content/www/us/en/developer/articles/license/end-user-license-agreement.html](https://www.intel.com/content/www/us/en/developer/articles/license/end-user-license-agreement.html)
   - Intel Runtime Redistribution License (iomp5) — same source.
2. Recompute the SHA256 of `NOTICE.md` and append it to `SHA256SUMS`.
3. Verify the final file links/text render correctly in GitHub preview.

### Acceptance

- `artifacts/legacy-runtime/imlp-1.0.0/NOTICE.md` exists, is committed, and lists every component in the inventory above with its license text or a stable URL.
- The Intel MKL-ML and `libiomp5` entries include the **full** Intel license text verbatim (not just a link).
- `SHA256SUMS` includes a checksum line for `NOTICE.md`.
- A reviewer reading only `NOTICE.md` can determine, for any single `.so`/`.dll` in the archive, which license it falls under and where to find the upstream source.

---

## Phase A — Publish rescued runtime to Zenodo

Status: implemented. The 647 MB archive plus its manifest and checksums were published as Zenodo record `20026836`.

Published metadata:
- Version DOI: <https://doi.org/10.5281/zenodo.20026836>
- Concept DOI: <https://doi.org/10.5281/zenodo.20025319>
- Record URL: <https://zenodo.org/records/20026836>
- Runtime file URL: <https://zenodo.org/api/records/20026836/files/legacy-runtime.tar.gz/content>
- Runtime SHA256: `f7e60cf2889aa2315bea989c3e3b86fdc70b75d2df0269fed4e4b93dd94bd4ed`
- Base-image source directory: ignored local [zenodo-record/](../zenodo-record/) staging directory and published Zenodo files.

The historical procedure is retained below for future versioned re-publication.

### Steps

1. Log in to Zenodo (or Sandbox first for dry-run).
2. Create a new deposition, type **Software**.
3. Upload, unmodified, from [artifacts/legacy-runtime/imlp-1.0.0/](artifacts/legacy-runtime/imlp-1.0.0/):
   - `legacy-runtime.tar.gz` (sha256 `f7e60cf2889aa2315bea989c3e3b86fdc70b75d2df0269fed4e4b93dd94bd4ed`)
   - `runtime-manifest.json`
   - `SHA256SUMS` (regenerated in Phase 0 to include `NOTICE.md`)
   - `NOTICE.md` (produced in Phase 0 — must accompany the tarball on Zenodo)
   - A short `README.md` (write inline in the deposition) describing: source image digest `csbdocker/imlp@sha256:c5781e2a198c7281e9fd4d46bff385c3f2c13b8eaa56538bc598f4bd249fdc34`, paths covered (`/usr/local/cntk/cntk/lib`, `/usr/local/cntk/cntk/dependencies/lib`, `/usr/local/mpi/lib`), required env vars, target platform `linux/amd64`, and a pointer to `NOTICE.md` for the full multi-license breakdown.
4. Metadata:
   - **Title**: "cntk-dotnet: docker runtime image base for running legacy cntk inference with .NET applications"
   - **Authors**: pull from [RELEASE_NOTES.md](RELEASE_NOTES.md) (Mühlhaus, Zimmer, Schneider).
   - **License**: select **"Other (Open)"** in Zenodo's license picker (Zenodo only allows one SPDX value per record; the archive bundles MIT, BSD-3-Clause, Apache-2.0, and Intel proprietary-but-redistributable components, so no single SPDX is honest). Reference `NOTICE.md` from the description as the authoritative license document. Do **not** label the record "MIT" alone — that would misrepresent the Apache-2.0/BSD-3/Intel-licensed components.
   - **Related identifiers**: link to the GitHub repo and to the original CNTK upstream.
   - **Version**: `1.0.1+cntk2.7`.
5. Publish. Record both:
   - **Concept DOI** (resolves to the latest version forever — use in human-readable docs)
   - **Version DOI** (immutable — use in Dockerfile pin)
6. Capture the **direct file URL** for `legacy-runtime.tar.gz` (for this record: `https://zenodo.org/api/records/20026836/files/legacy-runtime.tar.gz/content`).
7. Keep the published DOI, runtime URL, and SHA256 in this plan. The base Dockerfile itself is preserved in the Zenodo record rather than tracked in this repo.

### Acceptance

- Zenodo record is public and resolvable via DOI.
- `curl -L <file-url> | sha256sum` matches the recorded checksum.
- The published DOI, runtime URL, and SHA256 are recorded in this plan.
- The Zenodo record's file list includes `NOTICE.md` alongside the tarball, manifest, and `SHA256SUMS`.
- The Zenodo record's license field is set to "Other (Open)" and the description links to `NOTICE.md`.

---

## Phase B — Preserve the .NET 10 + CNTK base Dockerfile in Zenodo

Status: implemented in the Zenodo record, not tracked in this repo.

### File: `zenodo-record/Dockerfile`

Behaviour:

- `FROM mcr.microsoft.com/dotnet/runtime:10.0` (**build with `--platform linux/amd64`** — CNTK has no aarch64 build).
- Build context must contain `legacy-runtime.tar.gz`, `libnuma1_2.0.14-3ubuntu2_amd64.deb`, and `NOTICE.md`; the ignored local `zenodo-record/` staging directory contains these files for local publication.
- Build args:
  - `ARG RUNTIME_SHA256` — expected SHA256 for `legacy-runtime.tar.gz`.
  - `ARG LIBNUMA1_SHA256` — expected SHA256 for the vendored `libnuma1` Debian package.
  - `ARG ZENODO_DOI` — optional provenance label value.
  - `ARG IMAGE_VERSION` — OCI image version label value; currently `1.0.1+cntk2.7`.
- Logic:
  1. `COPY libnuma1_2.0.14-3ubuntu2_amd64.deb /tmp/libnuma1.deb`.
  2. `echo "${LIBNUMA1_SHA256}  /tmp/libnuma1.deb" | sha256sum -c -` (fail build on mismatch).
  3. `dpkg -i /tmp/libnuma1.deb && rm /tmp/libnuma1.deb`.
  4. `COPY legacy-runtime.tar.gz /tmp/legacy-runtime.tar.gz`.
  5. `COPY NOTICE.md /usr/local/share/cntk-runtime/NOTICE.md`.
  6. `echo "${RUNTIME_SHA256}  /tmp/legacy-runtime.tar.gz" | sha256sum -c -` (fail build on mismatch).
  7. `tar -xzf /tmp/legacy-runtime.tar.gz -C / && rm /tmp/legacy-runtime.tar.gz`.
- Env (carried over from current [Dockerfile:18-19](Dockerfile#L18-L19)):
  - `ENV PATH="/usr/local/cntk/cntk/lib:/usr/local/mpi/bin:${PATH}"`
  - `ENV LD_LIBRARY_PATH="/usr/local/cntk/cntk/dependencies/lib:/usr/local/cntk/cntk/lib:/usr/local/mpi/lib:${LD_LIBRARY_PATH}"`
- OCI labels: `org.opencontainers.image.source`, `.url`, `.ref.name`, `.version`, `.licenses`, plus custom `org.csbdocker.cntk.*` labels.

### Tagging strategy on Docker Hub

Repository: `csbdocker/cntk-dotnet`.

Tags pushed locally from `zenodo-record/`:
- `1.0.1-cntk2.7-dotnet10` — fully pinned, immutable.
- `cntk2.7-dotnet10` — moves with the latest CNTK 2.7 on .NET 10 build.
- `dotnet10` — moves with the latest CNTK-backed .NET 10 build.
- `latest` — moves with the newest base build overall.

### Acceptance

- `docker build --platform linux/amd64 ... .` succeeds from `zenodo-record/`.
- `docker run --rm <image> ls /usr/local/cntk/cntk/lib` lists the CNTK shared libs.
- `docker run --rm <image> dotnet --info` shows .NET 10.
- Re-running after downloading the same Zenodo payload produces the same extracted runtime layer (modulo timestamps).

---

## Phase C — Refactor [Dockerfile](Dockerfile) and retarget DPPOP.CLI to net10

### Project changes

- [global.json](global.json): keep `sdk.version` on `10.0.100`; keep `rollForward: latestMinor`.
- [src/DPPOP.CLI/DPPOP.CLI.fsproj:5](src/DPPOP.CLI/DPPOP.CLI.fsproj#L5): use `<TargetFramework>net10.0</TargetFramework>`.
- [src/BioFSharp.ML/BioFSharp.ML.fsproj](src/BioFSharp.ML/BioFSharp.ML.fsproj): **no change** — `netstandard2.0` is consumed fine by net10.0.
- Test projects under `tests/` should target net10.0 with the CLI.
- Confirm `CNTK.CPUOnly 2.7` still loads under net10.0. The native libs are loaded by P/Invoke against the rescued `.so` files independently of the NuGet; verify in Phase E.

### Refactored top-level [Dockerfile](Dockerfile)

Two stages, both leaning on the new base:

```
ARG BASE_IMAGE=csbdocker/cntk-dotnet:1.0.1-cntk2.7-dotnet10

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/DPPOP.CLI/DPPOP.CLI.fsproj -c Release -o /app/publish

FROM ${BASE_IMAGE}
WORKDIR /app
COPY --from=build /app/publish ./
ENTRYPOINT ["dotnet", "DPPOP.CLI.dll"]
```

This deletes the entire `apt-get install libnuma1` + `tar -xzf legacy-runtime.tar.gz` block and the `ENV PATH`/`LD_LIBRARY_PATH` lines from the current Dockerfile — they all live in the base now.

### Acceptance

- `.\build.cmd RunTests` (per [AGENTS.md](AGENTS.md)) passes locally on a .NET 10 SDK.
- `docker build -t csbdocker/dppop .` (using the published base from Docker Hub) succeeds with no local `artifacts/` content present.
- `docker run --rm --mount type=bind,source=...,target=/data csbdocker/dppop --proteome ... --proteins-of-interest ... --model nonplant --output /data/results.tsv` produces output matching the existing golden tests in [tests/DPPOP.Tests](tests/DPPOP.Tests).

---

## Phase D — Build locally and publish base image to Docker Hub

Build and publish the base image from the ignored local `zenodo-record/` directory after the Zenodo record is public.

### Local publish steps

1. Enter the local Zenodo staging directory:
   `cd zenodo-record`
2. Verify the record payload:
   `sha256sum -c SHA256SUMS`
3. Build the image:
   `docker build --platform linux/amd64 --build-arg ZENODO_DOI=10.5281/zenodo.20026836 --build-arg IMAGE_VERSION=1.0.1+cntk2.7 -t csbdocker/cntk-dotnet:1.0.1-cntk2.7-dotnet10 -t csbdocker/cntk-dotnet:cntk2.7-dotnet10 -t csbdocker/cntk-dotnet:dotnet10 -t csbdocker/cntk-dotnet:latest .`
4. Smoke-test the local image:
   `docker run --rm csbdocker/cntk-dotnet:1.0.1-cntk2.7-dotnet10 dotnet --info`
   `docker run --rm csbdocker/cntk-dotnet:1.0.1-cntk2.7-dotnet10 ls /usr/local/cntk/cntk/lib`
5. Log in and push all tags:
   `docker login`
   `docker push csbdocker/cntk-dotnet:1.0.1-cntk2.7-dotnet10`
   `docker push csbdocker/cntk-dotnet:cntk2.7-dotnet10`
   `docker push csbdocker/cntk-dotnet:dotnet10`
   `docker push csbdocker/cntk-dotnet:latest`

No multi-arch publish — CNTK is amd64-only and silent failure on aarch64 would be worse than refusing.

### Pre-publication checklist (one-time, manual)

- Confirm Docker Hub org `csbdocker` access and create/confirm the `csbdocker/cntk-dotnet` repository.
- Confirm Docker Desktop or the local Docker daemon can build `linux/amd64`.

### Acceptance

- Local build/push produces all four tags on Docker Hub.
- `docker pull csbdocker/cntk-dotnet:1.0.1-cntk2.7-dotnet10` works from a clean machine.
- The pulled image digest matches the local build digest recorded during publication.

---

## Phase E — Documentation updates

Minimal, in-place edits — no new top-level docs:

- [AGENTS.md](AGENTS.md) "Container Workflow" and "Legacy CNTK Runtime" sections: replace the rescue-then-build narrative with "pull `csbdocker/cntk-dotnet:1.0.1-cntk2.7-dotnet10` then `docker build`"; keep the rescue script documented as the procedure used to *produce* the Zenodo upload, not a per-build prerequisite.
- [plans/rescue_modernize.md](plans/rescue_modernize.md) Phase 4 status: mark implemented and link to Zenodo DOI + Docker Hub repo.
- [README.md](README.md): add a one-paragraph "Container" section pointing at `csbdocker/cntk-dotnet` and the Zenodo DOI badge.

---

## Critical files

- New: `artifacts/legacy-runtime/imlp-1.0.0/NOTICE.md` (Phase 0), ignored `zenodo-record/Dockerfile` staged and published in Zenodo
- Modified: `artifacts/legacy-runtime/imlp-1.0.0/SHA256SUMS` (add NOTICE entry), [Dockerfile](Dockerfile), [global.json](global.json), [src/DPPOP.CLI/DPPOP.CLI.fsproj](src/DPPOP.CLI/DPPOP.CLI.fsproj), [AGENTS.md](AGENTS.md), [plans/rescue_modernize.md](plans/rescue_modernize.md), [README.md](README.md)
- Untouched but referenced: [artifacts/legacy-runtime/imlp-1.0.0/legacy-runtime.tar.gz](artifacts/legacy-runtime/imlp-1.0.0/legacy-runtime.tar.gz) (uploaded as-is to Zenodo), [scripts/Export-ImlpLegacyRuntime.ps1](scripts/Export-ImlpLegacyRuntime.ps1) (still the canonical procedure to regenerate the archive if the source image is ever re-rescued).

---

## End-to-end verification

Run in this order; each step gates the next.

1. **Zenodo record reachable**
   `curl -fL -o /tmp/r.tar.gz https://zenodo.org/api/records/20026836/files/legacy-runtime.tar.gz/content && sha256sum /tmp/r.tar.gz` matches the recorded SHA256.
2. **Base image context is present**
   `zenodo-record/` contains `Dockerfile`, `legacy-runtime.tar.gz`, `NOTICE.md`, `README.md`, `runtime-manifest.json`, and `SHA256SUMS`.
3. **Base image builds locally**
   `cd zenodo-record && docker build --platform linux/amd64 -t local/cntk-dotnet:cntk2.7-dotnet10 --build-arg ZENODO_DOI=10.5281/zenodo.20026836 --build-arg IMAGE_VERSION=1.0.1+cntk2.7 .`
4. **CNTK loads under .NET 10**
   `docker run --rm local/cntk-dotnet:cntk2.7-dotnet10 dotnet --info` and `ls /usr/local/cntk/cntk/lib`.
5. **Tests pass after retarget**
   `.\build.cmd RunTests` on a host with .NET 10 SDK installed.
6. **DPPOP image builds on top of the base**
   `docker build --build-arg BASE_IMAGE=local/cntk-dotnet:cntk2.7-dotnet10 -t dppop:local .`
7. **DPPOP smoke test against existing fixtures**
   `docker run --rm --mount type=bind,source=$PWD/tests/Container/data,target=/data dppop:local --proteome /data/Chlamy_JGI5_5.fasta --proteins-of-interest /data/<targets> --model plant --output /data/results.tsv`; diff against the golden TSV used by [tests/DPPOP.Tests](tests/DPPOP.Tests).
8. **Public publish**
   Push `csbdocker/cntk-dotnet:{1.0.1-cntk2.7-dotnet10,cntk2.7-dotnet10,dotnet10,latest}`; pull from a clean machine and re-run step 7 against the published base.

---

## Risks and known unknowns

- **CNTK 2.7 managed shim under .NET 10** — verify before publishing. If `Cntk.Core.Managed-2.7.dll` fails to load, expect to add a binding redirect or a small interop shim. Discoverable at step 4/5.
- **Docker Hub org access** — the `csbdocker` namespace must be available to the publishing credentials. Phase D depends on this.
- **Zenodo Software upload size** — 647 MB is well under the 50 GB per-record limit; no issue, but uploads over ~100 MB go via the new files API and a slow connection could take a while.
- **amd64-only** — explicit and intentional; documented in the base image labels and in the README.
- **Intel MKL-ML / `libiomp5` redistribution terms** — these are the only proprietary components in the archive. Microsoft shipped them in the public CNTK 2.7 Linux release under Intel's runtime redistribution grant; redistributing the same unmodified files via Zenodo is the same kind of redistribution. The risk is low but non-zero, and is mitigated by reproducing the full Intel license texts verbatim in `NOTICE.md` (Phase 0). If at any point a redistribution objection is raised, the fallback is to strip those two files from the archive at the cost of CNTK math performance.
- **NOTICE drift** — if the archive is ever re-rescued from a different source image, `NOTICE.md` must be regenerated to match. Document this expectation alongside [scripts/Export-ImlpLegacyRuntime.ps1](scripts/Export-ImlpLegacyRuntime.ps1).
