# BioFSharp.ML

![Logo](docs/img/Logo_large.png)

A template repository for creating an extension package for BioFSharp.

## Content

- `src/BioFSharp.ML`: The main project folder. Contains a library with BioFSharp core dependency.
- `tests/BioFSharp.ML.Tests`: The test project folder. Contains a XUnit test project
- `build/build.fsproj`: A FAKE build project that handles building, testing, packaging, publishing, etc.
- `docs`: the docs folder contains an example index.fsx file with simple documentation boilerplate.

## Setup

Here is a list of things you should/might want to do after setting up a repo with this template:

> [!IMPORTANT]  
> Whenever you change a project file name or folder, make sure to fix the solution registration afterwards.

- Rename some things: Replace `XYZ` with the name of your package
  - `PackageTemplate.sln`
  - `src/BioFSharp.ML`
  - `src/BioFSharp.ML/BioFSharp.ML.fsproj`
    - Rename and add nuget package metadata
  - `tests/BioFSharp.ML`
  - `tests/BioFSharp.ML.Tests/BioFSharp.ML.Tests.fsproj`
    - Also make sure to fix the project reference to BioFSharp.ML when renamed
  - in `build/ProjectInfo.fs`:
    - Set project name: 
      ```fsharp
      let project = "BioFSharp.ML" // replace with the name of your project
      ```
    - Set git owner:
      ```fsharp
      let gitOwner = "BioFSharp" // replace with github account name or organization where repo is hosted if necessary
      ```
    - fix test project path:
      ```fsharp
      let testProjects = 
      [
          "tests/BioFSharp.ML.Tests/BioFSharp.ML.Tests.fsproj" // replace with the name of your test project
      ]
      ```
  - in `.github/workflows/build-and-test.yml`: change codecov slug
- If needed, change the target framework of the project. it currently targets `.netstandard2.0` for maximum backwards compatibility, might want to target a newer `.net` version if you need a specific API.

## Development

### General

BioFSharp repositories usually folllow this structure:

```
root
│   📄<project name>.sln
│   📄build.cmd
│   📄build.sh
├───📁build
├───📁docs
├───📁src
|   └───📁<project name>
└───tests
    └───📁<testproject name>
```

- <project name>.sln is the root solution file.
- `build` contains a [FAKE](https://fake.build/) build project with targets for building, testing and packaging the project.
- `build/sh` and `build.cmd` in the root are shorthand scripts to execute the buildproject.
- `docs` contains the documentation in form of literate scripts and notebooks. 
- `src` contains folders with the source code of the project(s).
- `tests` contains folders with test projects.

### Build

just call `build.sh` or `build.cmd` depending on your OS.

### Test

```bash
build.sh runtests
```

```bash
build.cmd runtests
```

### Create Nuget package

```bash
build.sh pack
```
```bash
build.cmd pack
```

### Docs

You can watch locally with hot reload via

```bash
build.sh watchdocs
```
```bash
build.cmd watchdocs
```