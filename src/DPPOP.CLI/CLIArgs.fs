namespace DPPOP.CLI

open Argu

type CLIArguments =
    | [<Unique; Mandatory; AltCommandLine("-p")>] Proteome of path:string
    | [<Unique; Mandatory; AltCommandLine("-i")>] Proteins_Of_Interest of path:string
    | [<Unique; AltCommandLine("-o")>] Output of path:string
    | [<Unique; AltCommandLine("-m")>] Model of value:string
    | [<Unique; AltCommandLine("-c")>] Custom_Model of path:string
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Proteome _ -> "Path to the FASTA proteome used to determine distinct tryptic peptides."
            | Proteins_Of_Interest _ -> "Path to the FASTA file containing proteins to score."
            | Output _ -> "Optional output path for TSV results. Defaults to stdout."
            | Model _ -> "Prediction profile to use for normalization: plant or nonplant. Defaults to nonplant."
            | Custom_Model _ -> "Optional path to a custom CNTK model. The selected model profile still controls normalization."
