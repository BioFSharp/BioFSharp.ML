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

## Build

This repo contains a buildproject that can be called either via `build.cmd`, `build.sh`, or directly via `dotnet run`.

Find all build targets in `build/Build.fs`.

Examples:

- run the default build target (`Build`) via build.cmd:
  ```bash
  ./build.cmd
  ```
- run the `RunTests` target in build.sh:     
  ```bash
  ./build.sh RunTests
  ```