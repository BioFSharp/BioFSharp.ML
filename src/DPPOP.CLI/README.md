# DPPOP.CLI

`DPPOP.CLI` packages the peptide observability workflow from `BioFSharp.ML.DPPOP` as a command-line tool.

## Usage

```powershell
dppop --proteome proteome.fasta --proteins-of-interest targets.fasta --model nonplant --output results.tsv
```

## Container

Build the container from the repository root after publishing or pulling the CNTK/.NET base image:

```powershell
docker build -t dppop .
```

Run it with mounted input data under `/data`:

```powershell
docker run --rm --mount "type=bind,source=C:/my-data,target=/data" dppop --proteome /data/proteome.fasta --proteins-of-interest /data/targets.fasta --output /data/results.tsv
```

### Local container smoke test

From the repository root, build the image and run it against the checked-in
container fixtures:

```powershell
docker build -t dppop:local .

docker run --rm `
  --mount "type=bind,source=${PWD}\tests\Container\data,target=/data" `
  dppop:local `
  --proteome /data/Chlamy_JGI5_5.fasta `
  --proteins-of-interest /data/rbcl.fasta `
  --model plant `
  --output /data/dppop-results.tsv
```

The command mounts `tests/Container/data` as writable so the output TSV is
created next to the two input FASTA files.

### Notes

- `--model` controls the normalization profile and accepts `plant` or `nonplant`.
- `--custom-model` can point to a custom CNTK model file while keeping the selected normalization profile.
- CNTK native dependencies are supplied by the `csbdocker/cntk-dotnet:1.0.1-cntk2.7-dotnet10` base image.
