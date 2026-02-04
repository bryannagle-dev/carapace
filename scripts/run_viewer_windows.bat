@echo off
setlocal

set SCRIPT_DIR=%~dp0
pushd "%SCRIPT_DIR%.."
set ROOT=%CD%
set GODOT=%ROOT%\tools\godot\Godot_v4.6-stable_mono_win64\Godot_v4.6-stable_mono_win64.exe
set VXM=%ROOT%\output\torso_only.vxm

if not exist "%GODOT%" (
  echo Godot binary not found. Running get_godot.ps1...
  powershell -ExecutionPolicy Bypass -File "%ROOT%\get_godot.ps1"
)

if not exist "%GODOT%" (
  echo Godot binary still not found. Please run get_godot.ps1 manually.
  pause
  popd
  exit /b 1
)

if not exist "%VXM%" (
  echo VXM file not found. Generate it in WSL first.
  pause
  popd
  exit /b 1
)

"%GODOT%" --path "%ROOT%\src\Viewer" -- --file "%VXM%"
popd
endlocal
