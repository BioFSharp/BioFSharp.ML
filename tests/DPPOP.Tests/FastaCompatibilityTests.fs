namespace DPPOP.Tests

open System
open System.IO
open Xunit
open BioFSharp
open DPPOP.CLI

module private FastaCompatibilityTestData =

    let withTemporaryFasta contents run =
        let tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.fasta")

        try
            File.WriteAllText(tempPath, contents)
            run tempPath
        finally
            if File.Exists(tempPath) then
                File.Delete(tempPath)

type FastaCompatibilityTests() =

    [<Fact>]
    member _.``CLI input sanitation removes illegal characters and reports them`` () =
        let fastaContents =
            String.concat
                Environment.NewLine
                [|
                    ">test-protein"
                    "MDATSKADLPDYAADNRLPPWLLPDQEGKPAGRHLHYRPDILLIPSISLAAALNPDFVVLPSERDTIHIIEAGYTADTNHAAKQHEKAQQQQALAADLREAGWKVQYTPQSAISLGFAGTIRKDLHPLLTSLPTKPGSAATPYTTTQSPPSTT-LS*"
                |]

        FastaCompatibilityTestData.withTemporaryFasta fastaContents <| fun fastaPath ->
            let result = Input.readFastaWithSanitation fastaPath

            Assert.True(result.EncounteredIllegalCharacters)

            let entry = Assert.Single(result.Entries)

            let sanitizedSequence =
                entry.Sequence
                |> Array.ofSeq
                |> BioArray.toString

            Assert.DoesNotContain("*", sanitizedSequence)
            Assert.DoesNotContain("-", sanitizedSequence)
            Assert.EndsWith("TTLS", sanitizedSequence)
