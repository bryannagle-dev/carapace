#!/usr/bin/env bash
set -euo pipefail

VERSION="4.6-stable"
TEMPLATE_DIR="4.6.stable.mono"
BASE_URL="https://godot-releases.nbg1.your-objectstorage.com/${VERSION}"
ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DEST_DIR="${ROOT_DIR}/tools/godot"

EDITOR_ZIP="Godot_v${VERSION}_mono_linux_x86_64.zip"
TEMPLATES_TPZ="Godot_v${VERSION}_mono_export_templates.tpz"

mkdir -p "${DEST_DIR}"

fetch() {
  local url="$1"
  local out="$2"
  if command -v curl >/dev/null 2>&1; then
    curl -L --fail -o "${out}" "${url}"
  elif command -v wget >/dev/null 2>&1; then
    wget -O "${out}" "${url}"
  else
    echo "Error: curl or wget is required." >&2
    exit 1
  fi
}

extract_zip() {
  local zip_path="$1"
  local dest_dir="$2"
  if command -v unzip >/dev/null 2>&1; then
    unzip -q -o "${zip_path}" -d "${dest_dir}"
  elif command -v python3 >/dev/null 2>&1; then
    python3 -m zipfile -e "${zip_path}" "${dest_dir}"
  else
    echo "Error: unzip or python3 is required to extract ${zip_path}." >&2
    exit 1
  fi
}

if [ ! -f "${DEST_DIR}/${EDITOR_ZIP}" ]; then
  fetch "${BASE_URL}/${EDITOR_ZIP}" "${DEST_DIR}/${EDITOR_ZIP}"
fi

if [ ! -d "${DEST_DIR}/Godot_v${VERSION}_mono_linux_x86_64" ]; then
  extract_zip "${DEST_DIR}/${EDITOR_ZIP}" "${DEST_DIR}"
  chmod +x "${DEST_DIR}/Godot_v${VERSION}_mono_linux_x86_64/Godot_v${VERSION}_mono_linux.x86_64"
fi

if [ ! -f "${DEST_DIR}/${TEMPLATES_TPZ}" ]; then
  fetch "${BASE_URL}/${TEMPLATES_TPZ}" "${DEST_DIR}/${TEMPLATES_TPZ}"
fi

if [ ! -d "${DEST_DIR}/export-templates/${TEMPLATE_DIR}" ]; then
  mkdir -p "${DEST_DIR}/export-templates/${TEMPLATE_DIR}"
  extract_zip "${DEST_DIR}/${TEMPLATES_TPZ}" "${DEST_DIR}/export-templates/${TEMPLATE_DIR}"
fi

echo "Godot ${VERSION} .NET editor downloaded to: ${DEST_DIR}/Godot_v${VERSION}_mono_linux_x86_64"
echo "Export templates extracted to: ${DEST_DIR}/export-templates/${TEMPLATE_DIR}"
