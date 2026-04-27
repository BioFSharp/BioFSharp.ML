namespace DPPOP.CLI

open System
open System.IO
open System.Text
open BioFSharp
open BioFSharp.IO

module Input =

    type ReadFastaResult<'T> = {
        Entries: 'T array
        EncounteredIllegalCharacters: bool
    }

    let private isIllegalSequenceCharacter (c: char) =
        c = '*' || c = '-'

    let readFastaWithSanitation path =
        let sanitizedBuilder = StringBuilder()
        let mutable encounteredIllegalCharacters = false

        for rawLine in File.ReadLines(path) do
            let line = rawLine.Trim()
            if line.StartsWith(">", StringComparison.Ordinal) then
                sanitizedBuilder.AppendLine(line) |> ignore
            elif String.IsNullOrWhiteSpace(line) then
                ()
            else
                let sanitizedLine =
                    line
                    |> Seq.filter (fun c ->
                        let isIllegal = isIllegalSequenceCharacter c
                        if isIllegal then
                            encounteredIllegalCharacters <- true
                        not isIllegal
                    )
                    |> Array.ofSeq
                    |> String

                sanitizedBuilder.AppendLine(sanitizedLine) |> ignore

        let tempPath = Path.GetTempFileName()

        try
            File.WriteAllText(tempPath, sanitizedBuilder.ToString())

            {
                Entries =
                    tempPath
                    |> Fasta.read BioArray.ofAminoAcidString
                    |> Array.ofSeq
                EncounteredIllegalCharacters = encounteredIllegalCharacters
            }
        finally
            if File.Exists(tempPath) then
                File.Delete(tempPath)
