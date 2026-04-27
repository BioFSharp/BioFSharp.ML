namespace DPPOP.Tests

open System
open System.IO
open Xunit
open BioFSharp
open DPPOP.CLI

module private TestPaths =

    let repoRoot =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."))

    let testFasta =
        Path.Combine(repoRoot, "src", "BioFSharp.ML", "Resources", "test.fasta")

type FastaCompatibilityTests() =

    [<Fact>]
    member _.``CLI input sanitation removes illegal characters and reports them`` () =
        Assert.True(
            File.Exists(TestPaths.testFasta),
            $"Expected test FASTA at '{TestPaths.testFasta}'."
        )

        let result = Input.readFastaWithSanitation TestPaths.testFasta

        Assert.True(result.EncounteredIllegalCharacters)
        Assert.Single(result.Entries) |> ignore

        let sanitizedSequence =
            result.Entries
            |> Array.exactlyOne
            |> fun entry -> entry.Sequence
            |> Array.ofSeq
            |> BioArray.toString

        Assert.DoesNotContain("*", sanitizedSequence)
        Assert.DoesNotContain("-", sanitizedSequence)
        Assert.EndsWith("TTLS", sanitizedSequence)
