module DPPOP.CLI.Program

open System
open System.IO
open Argu
open BioFSharp
open BioFSharp.IO
open BioFSharp.ML.DPPOP
open DPPOP.CLI

module private App =

    type ModelProfile =
        | PlantProfile
        | NonPlantProfile

    type AppConfig = {
        ProteomePath          : string
        ProteinsOfInterestPath: string
        OutputPath            : string option
        ModelProfile          : ModelProfile
        CustomModelPath       : string option
    }

    let private parseModelProfile = function
        | None -> NonPlantProfile
        | Some value when String.Equals(value, "plant", StringComparison.OrdinalIgnoreCase) -> PlantProfile
        | Some value when String.Equals(value, "nonplant", StringComparison.OrdinalIgnoreCase) -> NonPlantProfile
        | Some value when String.Equals(value, "non-plant", StringComparison.OrdinalIgnoreCase) -> NonPlantProfile
        | Some value -> invalidArg "model" $"Unsupported model profile '{value}'. Use 'plant' or 'nonplant'."

    let private ensureFileExists label path =
        if not (File.Exists(path)) then
            invalidArg label $"Could not find {label} file at '{path}'."

    let private ensureOutputDirectory path =
        let fullPath = Path.GetFullPath(path)
        let directory = Path.GetDirectoryName(fullPath)
        if not (String.IsNullOrWhiteSpace(directory)) then
            Directory.CreateDirectory(directory) |> ignore

    let private sanitizeTsvValue (value:string) =
        value.Replace("\t", " ").Replace("\r", " ").Replace("\n", " ")

    let private getPredictionModel config =
        match config.CustomModelPath with
        | Some modelPath -> Prediction.Model.Custom modelPath
        | None ->
            match config.ModelProfile with
            | PlantProfile -> Prediction.Model.Plant
            | NonPlantProfile -> Prediction.Model.NonPlant

    let private getNormalization config =
        match config.ModelProfile with
        | PlantProfile -> Classification.zNormalizePlantFeatureVector
        | NonPlantProfile -> Classification.zNormalizeNonPlantFeatureVector

    let private toTsv (results: PredictionOutput array) =
        let header = "ProteinId\tSequence\tPredictionScore\tDistinct"
        let rows =
            results
            |> Array.map (fun result ->
                $"{sanitizeTsvValue result.ProteinId}\t{result.Sequence}\t{result.PredictionScore:G17}\t{result.Distinct}"
            )
        String.concat Environment.NewLine (Array.append [| header |] rows)

    let buildConfig (parseResults: ParseResults<CLIArguments>) =
        let customModelPath = parseResults.TryGetResult CLIArguments.Custom_Model
        customModelPath |> Option.iter (ensureFileExists "custom model")

        {
            ProteomePath = parseResults.GetResult CLIArguments.Proteome
            ProteinsOfInterestPath = parseResults.GetResult CLIArguments.Proteins_Of_Interest
            OutputPath = parseResults.TryGetResult CLIArguments.Output
            ModelProfile = parseResults.TryGetResult CLIArguments.Model |> parseModelProfile
            CustomModelPath = customModelPath
        }

    let run config =
        ensureFileExists "proteome" config.ProteomePath
        ensureFileExists "proteins-of-interest" config.ProteinsOfInterestPath

        let proteome = Input.readFastaWithSanitation config.ProteomePath
        let proteinsOfInterest = Input.readFastaWithSanitation config.ProteinsOfInterestPath

        if proteome.EncounteredIllegalCharacters || proteinsOfInterest.EncounteredIllegalCharacters then
            eprintfn "Warning: input FASTA contained one or more illegal sequence characters ('*' or '-') which were removed before prediction."

        let predictionModel = getPredictionModel config
        let normalization = getNormalization config

        let results =
            Prediction.scoreProteinsAgainstProteome predictionModel normalization proteome.Entries proteinsOfInterest.Entries
            |> Seq.collect id
            |> Array.ofSeq

        let tsv = toTsv results

        match config.OutputPath with
        | Some outputPath ->
            ensureOutputDirectory outputPath
            File.WriteAllText(outputPath, tsv)
        | None ->
            printfn "%s" tsv

[<EntryPoint>]
let main argv =
    let parser = ArgumentParser.Create<CLIArguments>(programName = "dppop")

    try
        let parseResults = parser.ParseCommandLine(inputs = argv, raiseOnUsage = true)
        parseResults
        |> App.buildConfig
        |> App.run
        0
    with
    | :? ArguParseException as ex ->
        match ex.ErrorCode with
        | ErrorCode.HelpText ->
            printfn "%s" ex.Message
            0
        | _ ->
            eprintfn "%s" ex.Message
            eprintfn "%s" (parser.PrintUsage())
            1
    | ex ->
        eprintfn "%s" ex.Message
        1
