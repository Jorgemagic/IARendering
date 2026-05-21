<#
.SYNOPSIS
Downloads the Stable Diffusion runtime used by this repository.

.DESCRIPTION
This script recreates the expected StableDiffusion folder layout by downloading:
- Flux.2 Klein model files
- sd-cli CUDA runtime
- CUDA runtime dependencies for sd-cli
- sd-cli AVX2 runtime

Run from PowerShell:
  powershell -ExecutionPolicy Bypass -File .\Install-StableDiffusion.ps1

Use -Force to re-download and overwrite existing files.
#>

[CmdletBinding()]
param(
    [switch]$Force,
    [switch]$SkipCuda,
    [switch]$SkipCpu
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$modelDirectory = Join-Path $scriptRoot 'models\FluxKlein'
$capturesDirectory = Join-Path $scriptRoot 'captures'
$resultsDirectory = Join-Path $scriptRoot 'results'
$cudaDirectory = Join-Path $scriptRoot 'sd-cli-cuda'
$cpuDirectory = Join-Path $scriptRoot 'sd-cli-avx2'
$cudaDependencyMarkerPath = Join-Path $cudaDirectory '.cuda-dependencies-installed'
$temporaryRoot = Join-Path ([IO.Path]::GetTempPath()) ("IARendering-StableDiffusion-" + [Guid]::NewGuid().ToString('N'))

function Write-Step {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Message
    )

    Write-Host ''
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Ensure-Directory {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        New-Item -ItemType Directory -Path $Path | Out-Null
    }
}

function Download-File {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,
        [Parameter(Mandatory = $true)]
        [string]$Url,
        [Parameter(Mandatory = $true)]
        [string]$Destination,
        [switch]$ForceDownload
    )

    $parentDirectory = Split-Path -Parent $Destination
    if (-not [string]::IsNullOrWhiteSpace($parentDirectory)) {
        Ensure-Directory -Path $parentDirectory
    }

    if ((Test-Path -LiteralPath $Destination) -and -not $ForceDownload) {
        Write-Host "Skipping $Name because it already exists." -ForegroundColor Yellow
        return
    }

    if (Test-Path -LiteralPath $Destination) {
        Remove-Item -LiteralPath $Destination -Force
    }

    Write-Host "Downloading $Name..."

    $bitsTransfer = Get-Command -Name Start-BitsTransfer -ErrorAction SilentlyContinue
    if ($bitsTransfer) {
        try {
            Start-BitsTransfer -Source $Url -Destination $Destination -DisplayName $Name -Description $Name -ErrorAction Stop
            return
        }
        catch {
            Write-Warning "BITS download failed for $Name. Falling back to Invoke-WebRequest."

            if (Test-Path -LiteralPath $Destination) {
                Remove-Item -LiteralPath $Destination -Force
            }
        }
    }

    Invoke-WebRequest -Uri $Url -OutFile $Destination
}

function Expand-ZipArchive {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ArchivePath,
        [Parameter(Mandatory = $true)]
        [string]$Destination
    )

    Ensure-Directory -Path $Destination

    $extractDirectory = Join-Path $temporaryRoot ([IO.Path]::GetFileNameWithoutExtension($ArchivePath) + '-extract')
    if (Test-Path -LiteralPath $extractDirectory) {
        Remove-Item -LiteralPath $extractDirectory -Recurse -Force
    }

    New-Item -ItemType Directory -Path $extractDirectory | Out-Null
    Expand-Archive -LiteralPath $ArchivePath -DestinationPath $extractDirectory -Force

    $rootItems = @(Get-ChildItem -LiteralPath $extractDirectory -Force)
    $contentRoot = $extractDirectory

    if ($rootItems.Count -eq 1 -and $rootItems[0].PSIsContainer) {
        $contentRoot = $rootItems[0].FullName
    }

    foreach ($item in Get-ChildItem -LiteralPath $contentRoot -Force) {
        $targetPath = Join-Path $Destination $item.Name

        if ($item.PSIsContainer) {
            Copy-Item -LiteralPath $item.FullName -Destination $targetPath -Recurse -Force
        }
        else {
            Copy-Item -LiteralPath $item.FullName -Destination $targetPath -Force
        }
    }
}

$modelDownloads = @(
    @{
        Name = 'Flux.2 Klein diffuse model'
        Url = 'https://huggingface.co/leejet/FLUX.2-klein-4B-GGUF/resolve/main/flux-2-klein-4b-Q8_0.gguf?download=true'
        Destination = Join-Path $modelDirectory 'flux-2-klein-4b-Q8_0.gguf'
    },
    @{
        Name = 'Flux.2 Klein VAE'
        Url = 'https://huggingface.co/black-forest-labs/FLUX.2-small-decoder/resolve/main/full_encoder_small_decoder.safetensors?download=true'
        Destination = Join-Path $modelDirectory 'full_encoder_small_decoder.safetensors'
    },
    @{
        Name = 'Flux.2 Klein LLM'
        Url = 'https://huggingface.co/unsloth/Qwen3-4B-GGUF/resolve/main/Qwen3-4B-Q4_K_M.gguf?download=true'
        Destination = Join-Path $modelDirectory 'Qwen3-4B-Q4_K_M.gguf'
    }
)

$runtimeDownloads = @()

if (-not $SkipCuda) {
    $runtimeDownloads += @{
        Name = 'sd-cli CUDA runtime'
        Url = 'https://github.com/leejet/stable-diffusion.cpp/releases/download/master-633-5b0267e/sd-master-5b0267e-bin-win-cuda12-x64.zip'
        ArchiveName = 'sd-cli-cuda.zip'
        Destination = $cudaDirectory
        MarkerPath = Join-Path $cudaDirectory 'sd-cli.exe'
    }
}

if (-not $SkipCpu) {
    $runtimeDownloads += @{
        Name = 'sd-cli AVX2 runtime'
        Url = 'https://github.com/leejet/stable-diffusion.cpp/releases/download/master-633-5b0267e/sd-master-5b0267e-bin-win-avx2-x64.zip'
        ArchiveName = 'sd-cli-avx2.zip'
        Destination = $cpuDirectory
        MarkerPath = Join-Path $cpuDirectory 'sd-cli.exe'
    }
}

$cudaDependencyDownload = $null

if (-not $SkipCuda) {
    $cudaDependencyDownload = @{
        Name = 'sd-cli CUDA runtime dependencies'
        Url = 'https://github.com/leejet/stable-diffusion.cpp/releases/download/master-633-5b0267e/cudart-sd-bin-win-cu12-x64.zip'
        ArchiveName = 'sd-cli-cudart.zip'
        Destination = $cudaDirectory
        MarkerPath = $cudaDependencyMarkerPath
    }
}

try {
    Write-Step 'Preparing folder structure'
    Ensure-Directory -Path $temporaryRoot
    Ensure-Directory -Path $modelDirectory
    Ensure-Directory -Path $capturesDirectory
    Ensure-Directory -Path $resultsDirectory

    Write-Step 'Downloading Flux.2 Klein models'
    foreach ($download in $modelDownloads) {
        Download-File -Name $download.Name -Url $download.Url -Destination $download.Destination -ForceDownload:$Force
    }

    if ($runtimeDownloads.Count -gt 0) {
        Write-Step 'Downloading and extracting sd-cli runtimes'
    }

    foreach ($runtime in $runtimeDownloads) {
        if ((Test-Path -LiteralPath $runtime.MarkerPath) -and -not $Force) {
            Write-Host "Skipping $($runtime.Name) because it already exists." -ForegroundColor Yellow
            continue
        }

        $archivePath = Join-Path $temporaryRoot $runtime.ArchiveName
        Download-File -Name $runtime.Name -Url $runtime.Url -Destination $archivePath -ForceDownload:$true
        Expand-ZipArchive -ArchivePath $archivePath -Destination $runtime.Destination
    }

    if ($null -ne $cudaDependencyDownload) {
        Write-Step 'Downloading CUDA dependencies for sd-cli'

        if ((-not (Test-Path -LiteralPath $cudaDependencyDownload.MarkerPath)) -or $Force) {
            $archivePath = Join-Path $temporaryRoot $cudaDependencyDownload.ArchiveName
            Download-File -Name $cudaDependencyDownload.Name -Url $cudaDependencyDownload.Url -Destination $archivePath -ForceDownload:$true
            Expand-ZipArchive -ArchivePath $archivePath -Destination $cudaDependencyDownload.Destination
            Set-Content -LiteralPath $cudaDependencyDownload.MarkerPath -Value 'installed' -NoNewline
        }
        else {
            Write-Host "Skipping $($cudaDependencyDownload.Name) because it already exists." -ForegroundColor Yellow
        }
    }

    Write-Step 'Completed successfully'
    Write-Host 'StableDiffusion runtime is ready.'
    Write-Host "Location: $scriptRoot"
}
finally {
    if (Test-Path -LiteralPath $temporaryRoot) {
        Remove-Item -LiteralPath $temporaryRoot -Recurse -Force
    }
}
