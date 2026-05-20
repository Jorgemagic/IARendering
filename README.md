# IARendering

IARendering is a desktop application for viewing 3D models and generating an AI-based reinterpretation of the current viewport capture.

The project is built using **Evergine + Avalonia**:

- **Evergine** handles 3D rendering, asset loading, and scene integration.
- **Avalonia** provides the desktop UI, configuration panels, and user interaction flow.

The application allows you to:

- Load 3D models using drag and drop.
- View formats such as `GLB`, `STL`, and `OBJ`.
- Capture the current viewport.
- Launch AI generation using `stable-diffusion.cpp`.
- Compare the original viewport capture with the generated result.

## Requirements

- Windows
- [.NET 10 SDK](https://dotnet.microsoft.com/)
- Visual Studio 2022 or any compatible .NET tooling
- Internet connection on first setup to download the Stable Diffusion models and runtimes

Optional:

- NVIDIA GPU with CUDA support to use the GPU runtime
- CPU with AVX2 support to use the CPU runtime

## Clone the Repository

```powershell
git clone <REPOSITORY_URL>
cd IARendering
```

## Install Stable Diffusion

The binaries and models required for AI generation are not included in the repository because they take several GB of space. Instead, the project includes a script that automatically recreates the `StableDiffusion` folder structure.

Run this command before starting the application:

```powershell
powershell -ExecutionPolicy Bypass -File .\StableDiffusion\Install-StableDiffusion.ps1
```

The script downloads and prepares:

- The `Flux.2 Klein` model
- The VAE
- The auxiliary LLM
- The CUDA `sd-cli` runtime
- The AVX2 `sd-cli` runtime

Useful script options:

- `-Force` re-downloads and overwrites existing files.
- `-SkipCuda` skips the GPU runtime download.
- `-SkipCpu` skips the CPU runtime download.

Example:

```powershell
powershell -ExecutionPolicy Bypass -File .\StableDiffusion\Install-StableDiffusion.ps1 -SkipCuda
```

## Run the Project

### Option 1: Visual Studio

1. Open [IARendering.Avalonia.sln](/D:/Repositories/IARendering/IARendering.Avalonia.sln).
2. Restore NuGet packages if Visual Studio does not do it automatically.
3. Set `IARendering.Avalonia` as the startup project if needed.
4. Run in `Debug` or `Release`.

### Option 2: CLI

```powershell
dotnet restore .\IARendering.Avalonia.sln
dotnet run --project .\IARendering.Avalonia\IARendering.Avalonia.csproj
```

## Basic Workflow

1. Drag and drop a 3D model into the viewport.
2. Adjust the prompt and AI parameters from the side panel.
3. Choose the `GPU` or `CPU` runtime.
4. Click `Generate IA Render`.
5. Review the comparison between the viewport capture and the generated image.

## Relevant Structure

- [IARendering.Avalonia](/D:/Repositories/IARendering/IARendering.Avalonia): desktop application and Avalonia UI.
- [IARendering](/D:/Repositories/IARendering/IARendering): domain logic, Evergine integration, and AI generation.
- [StableDiffusion](/D:/Repositories/IARendering/StableDiffusion): local folder for models, runtimes, and generated outputs.

## Notes

- The first `StableDiffusion` installation can take a while because it downloads several GB of data.
- The downloaded contents inside `StableDiffusion` are ignored by Git and should not be committed.
- If you only work with CPU or only with GPU, you can use the script options to avoid unnecessary downloads.
