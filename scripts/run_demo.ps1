$ErrorActionPreference = "Stop"

$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
$Root = Split-Path -Parent $Root

$Godot = Join-Path $Root "tools/godot/Godot_v4.6-stable_mono_win64/Godot_v4.6-stable_mono_win64.exe"
$OutputDir = Join-Path $Root "output"
$OutFile = Join-Path $OutputDir "torso_only.vxm"

if (!(Test-Path $Godot)) {
  Write-Error "Godot binary not found. Run .\get_godot.ps1 first."
}

if (!(Test-Path (Join-Path $Root "src/Generator/VoxelGenerator.sln"))) {
  dotnet new sln -n VoxelGenerator -o (Join-Path $Root "src/Generator") | Out-Null
  dotnet sln (Join-Path $Root "src/Generator/VoxelGenerator.sln") add (Join-Path $Root "src/Generator/VoxelGenerator.csproj") | Out-Null
}

if (!(Test-Path (Join-Path $Root "src/Viewer/VoxelViewer.sln"))) {
  dotnet new sln -n VoxelViewer -o (Join-Path $Root "src/Viewer") | Out-Null
  dotnet sln (Join-Path $Root "src/Viewer/VoxelViewer.sln") add (Join-Path $Root "src/Viewer/VoxelViewer.csproj") | Out-Null
}

New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

dotnet build (Join-Path $Root "src/Generator/VoxelGenerator.csproj") -v minimal
dotnet build (Join-Path $Root "src/Viewer/VoxelViewer.csproj") -v minimal

& $Godot --headless --path (Join-Path $Root "src/Generator") -- --seed 123 --out $OutFile --height 48 --torso_voxels 1000 --style chunky
& $Godot --path (Join-Path $Root "src/Viewer") -- --file $OutFile
