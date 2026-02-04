$ErrorActionPreference = "Stop"

$Version = "4.6-stable"
$TemplateDir = "4.6.stable.mono"
$BaseUrl = "https://godot-releases.nbg1.your-objectstorage.com/$Version"

$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
$Dest = Join-Path $Root "tools/godot"

$EditorZip = "Godot_v${Version}_mono_win64.zip"
$TemplatesTpz = "Godot_v${Version}_mono_export_templates.tpz"

New-Item -ItemType Directory -Force -Path $Dest | Out-Null

[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

function Test-ZipFile {
  param([string]$Path)
  if (!(Test-Path $Path)) { return $false }
  try {
    $zip = [System.IO.Compression.ZipFile]::OpenRead($Path)
    $zip.Dispose()
    return $true
  } catch {
    return $false
  }
}

$EditorPath = Join-Path $Dest $EditorZip
if ((Test-Path $EditorPath) -and -not (Test-ZipFile $EditorPath)) {
  Remove-Item -Force $EditorPath
}
if (!(Test-Path $EditorPath)) {
  Invoke-WebRequest -Uri "$BaseUrl/$EditorZip" -OutFile $EditorPath
}

$EditorDir = Join-Path $Dest "Godot_v${Version}_mono_win64"
if (!(Test-Path $EditorDir)) {
  Expand-Archive -Force -Path $EditorPath -DestinationPath $Dest
}

$TemplatesPath = Join-Path $Dest $TemplatesTpz
if ((Test-Path $TemplatesPath) -and -not (Test-ZipFile $TemplatesPath)) {
  Remove-Item -Force $TemplatesPath
}
if (!(Test-Path $TemplatesPath)) {
  Invoke-WebRequest -Uri "$BaseUrl/$TemplatesTpz" -OutFile $TemplatesPath
}

$TemplateOut = Join-Path $Dest "export-templates/$TemplateDir"
if (!(Test-Path $TemplateOut)) {
  New-Item -ItemType Directory -Force -Path $TemplateOut | Out-Null
  Expand-Archive -Force -Path $TemplatesPath -DestinationPath $TemplateOut
}

Write-Host "Godot $Version .NET editor downloaded to: $EditorDir"
Write-Host "Export templates extracted to: $TemplateOut"
