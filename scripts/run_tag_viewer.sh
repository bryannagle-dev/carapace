#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
GODOT_BIN="${ROOT_DIR}/tools/godot/Godot_v4.6-stable_mono_linux_x86_64/Godot_v4.6-stable_mono_linux.x86_64"
FILE_PATH="${1:-${ROOT_DIR}/output/torso_only.vxm}"

if [ ! -x "${GODOT_BIN}" ]; then
  echo "Godot binary not found. Run ./get_godot.sh first." >&2
  exit 1
fi

if [ ! -f "${ROOT_DIR}/src/ViewerTags/VoxelTagViewer.sln" ]; then
  dotnet new sln -n VoxelTagViewer -o "${ROOT_DIR}/src/ViewerTags"
  dotnet sln "${ROOT_DIR}/src/ViewerTags/VoxelTagViewer.sln" add "${ROOT_DIR}/src/ViewerTags/VoxelTagViewer.csproj"
fi

dotnet build "${ROOT_DIR}/src/ViewerTags/VoxelTagViewer.csproj" -v minimal

"${GODOT_BIN}" --path "${ROOT_DIR}/src/ViewerTags" -- --file "${FILE_PATH}"
