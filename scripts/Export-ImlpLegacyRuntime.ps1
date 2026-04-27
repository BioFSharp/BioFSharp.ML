[CmdletBinding()]
param(
    [string]$Image = "csbdocker/imlp:1.0.0",
    [string]$OutputRoot = "artifacts/legacy-runtime/imlp-1.0.0"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Export-DockerStdoutToFile {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments,
        [Parameter(Mandatory = $true)]
        [string]$DestinationPath
    )

    $startInfo = New-Object System.Diagnostics.ProcessStartInfo
    $startInfo.FileName = "docker"
    $startInfo.Arguments = [string]::Join(" ", $Arguments)
    $startInfo.UseShellExecute = $false
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true

    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $startInfo
    [void]$process.Start()

    try {
        $fileStream = [System.IO.File]::Open($DestinationPath, [System.IO.FileMode]::Create, [System.IO.FileAccess]::Write, [System.IO.FileShare]::None)
        try {
            $process.StandardOutput.BaseStream.CopyTo($fileStream)
        }
        finally {
            $fileStream.Dispose()
        }

        $stderr = $process.StandardError.ReadToEnd()
        $process.WaitForExit()

        if ($process.ExitCode -ne 0) {
            if (Test-Path $DestinationPath) {
                Remove-Item -Force $DestinationPath
            }
            throw "docker $($Arguments -join ' ') failed with exit code $($process.ExitCode). $stderr"
        }
    }
    finally {
        $process.Dispose()
    }
}

function Get-ResolvedOutputRoot {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PathValue
    )

    if ([System.IO.Path]::IsPathRooted($PathValue)) {
        return $PathValue
    }

    $repoRoot = Split-Path -Parent $PSScriptRoot
    return Join-Path $repoRoot $PathValue
}

function New-Directory {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PathValue
    )

    New-Item -ItemType Directory -Force -Path $PathValue | Out-Null
}

$resolvedOutputRoot = Get-ResolvedOutputRoot -PathValue $OutputRoot
New-Directory -PathValue $resolvedOutputRoot

Write-Host "Inspecting image $Image ..."
$imageInspectRaw = & docker image inspect $Image
if ($LASTEXITCODE -ne 0) {
    throw "Could not inspect image '$Image'."
}
$imageInspect = $imageInspectRaw | ConvertFrom-Json

$archivePath = Join-Path $resolvedOutputRoot "legacy-runtime.tar.gz"
Write-Host "Exporting CNTK and OpenMPI runtime archive to $archivePath ..."
Export-DockerStdoutToFile `
    -Arguments @(
        "run",
        "--rm",
        "--entrypoint",
        "/bin/tar",
        $Image,
        "-C",
        "/",
        "-czf",
        "-",
        "usr/local/cntk/cntk/lib",
        "usr/local/cntk/cntk/dependencies/lib",
        "usr/local/mpi/lib"
    ) `
    -DestinationPath $archivePath

$manifest = [ordered]@{
    image            = $Image
    imageId          = $imageInspect[0].Id
    repoDigests      = $imageInspect[0].RepoDigests
    extractedAtUtc   = (Get-Date).ToUniversalTime().ToString("o")
    archive          = "legacy-runtime.tar.gz"
    extractedFrom    = [ordered]@{
        cntkLib             = "/usr/local/cntk/cntk/lib"
        cntkDependencyLib   = "/usr/local/cntk/cntk/dependencies/lib"
        mpiLib              = "/usr/local/mpi/lib"
    }
    requiredEnv      = [ordered]@{
        PATH            = "/usr/local/cntk/cntk/lib:/usr/local/mpi/bin:`$PATH"
        LD_LIBRARY_PATH = "/usr/local/cntk/cntk/dependencies/lib:/usr/local/cntk/cntk/lib:/usr/local/mpi/lib:`$LD_LIBRARY_PATH"
    }
}

$manifestPath = Join-Path $resolvedOutputRoot "runtime-manifest.json"
$manifest | ConvertTo-Json -Depth 6 | Set-Content -Path $manifestPath

$hashFilePath = Join-Path $resolvedOutputRoot "SHA256SUMS"
$rootPath = [System.IO.Path]::GetFullPath($resolvedOutputRoot)
$hashLines =
    Get-ChildItem -Path $resolvedOutputRoot -Recurse -File |
    Sort-Object FullName |
    ForEach-Object {
        $hash = (Get-FileHash -Path $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        $relativePath = $_.FullName.Substring($rootPath.Length).TrimStart('\').Replace('\', '/')
        "$hash *$relativePath"
    }
$hashLines | Set-Content -Path $hashFilePath

Write-Host "Legacy runtime exported to $resolvedOutputRoot"
Write-Host "Archive: $archivePath"
Write-Host "Manifest: $manifestPath"
Write-Host "Checksums: $hashFilePath"
