$ErrorActionPreference = "Stop"

$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
$Root = Split-Path -Parent $Root

$Godot = Join-Path $Root "tools\godot\Godot_v4.6-stable_mono_win64\Godot_v4.6-stable_mono_win64.exe"
$Vxm = Join-Path $Root "output\torso_only.vxm"

if (!(Test-Path $Godot)) {
  Write-Error "Godot binary not found. Run .\get_godot.ps1 first."
}

if (!(Test-Path $Vxm)) {
  Write-Error "VXM file not found. Generate it in WSL first."
}

& $Godot --path (Join-Path $Root "src\ViewerTags") -- --file $Vxm
