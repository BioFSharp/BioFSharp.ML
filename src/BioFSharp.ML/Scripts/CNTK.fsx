#r "nuget:"
#r "nuget:"
#r "nuget:"

#load "../CNTKLoadscript.fsx"
open CNTKLoadscript

CNTKLoadscript.resolveCNTKDependencies ()
#I "../../../bin/BioFSharp.ML/netstandard2.0"
#r "BioFSharp.dll"
#r "BioFSharp.IO"
#r "BioFSharp.ML"

open BioFSharp.ML.DPPOP
open BioFSharp.IO
open BioFSharp
open BioFSharp.IO.FastA
open FSharpAux
open FSharpAux.IO