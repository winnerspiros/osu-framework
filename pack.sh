#!/bin/bash
# One-click NuGet pack script for osu-framework.
# Usage: ./pack.sh [version] [--publish]
#
# Examples:
#   ./pack.sh                          # Packs with version 0.0.0-local
#   ./pack.sh 2026.420.1               # Packs with specified version
#   ./pack.sh 2026.420.1 --publish     # Packs and publishes to GitHub Packages (requires NUGET_API_KEY)
#
# Output: All .nupkg and .snupkg files are written to the ./artifacts/ directory.

set -euo pipefail

VERSION="${1:-0.0.0-local}"
PUBLISH=false

for arg in "$@"; do
    if [ "$arg" = "--publish" ]; then
        PUBLISH=true
    fi
done

ARTIFACTS_DIR="$(pwd)/artifacts"
COMMON_ARGS="-c Release /p:Version=${VERSION} /p:GenerateDocumentationFile=true"

echo "============================================="
echo " osu-framework NuGet Pack"
echo " Version : ${VERSION}"
echo " Output  : ${ARTIFACTS_DIR}"
echo "============================================="

# Clean artifacts
rm -rf "${ARTIFACTS_DIR}"
mkdir -p "${ARTIFACTS_DIR}"

echo ""
echo ">>> Packing osu.Framework (Desktop)..."
dotnet pack ${COMMON_ARGS} /p:IncludeSymbols=true /p:SymbolPackageFormat=snupkg \
    osu.Framework -o "${ARTIFACTS_DIR}"

echo ""
echo ">>> Packing osu.Framework.Android..."
if dotnet workload list 2>/dev/null | grep -q android; then
    dotnet pack ${COMMON_ARGS} \
        osu.Framework.Android -o "${ARTIFACTS_DIR}"
else
    echo "    [SKIP] Android workload not installed. Run 'dotnet workload install android' first."
fi

echo ""
echo ">>> Packing osu.Framework.iOS..."
if dotnet workload list 2>/dev/null | grep -q ios; then
    dotnet pack ${COMMON_ARGS} \
        osu.Framework.iOS -o "${ARTIFACTS_DIR}"
else
    echo "    [SKIP] iOS workload not installed. Run 'dotnet workload install ios' first."
fi

echo ""
echo "============================================="
echo " Packages created in ${ARTIFACTS_DIR}:"
ls -1 "${ARTIFACTS_DIR}"/*.nupkg 2>/dev/null || echo "  (none)"
ls -1 "${ARTIFACTS_DIR}"/*.snupkg 2>/dev/null || true
echo "============================================="

if [ "${PUBLISH}" = true ]; then
    echo ""
    echo ">>> Publishing packages..."
    if [ -z "${NUGET_API_KEY:-}" ]; then
        echo "Error: NUGET_API_KEY environment variable is not set."
        echo "Set it to your GitHub PAT or NuGet API key before running with --publish."
        exit 1
    fi

    if [ -z "${NUGET_SOURCE:-}" ]; then
        echo "Error: NUGET_SOURCE must be set for publishing."
        echo "Example: NUGET_SOURCE=https://nuget.pkg.github.com/<owner>/index.json"
        exit 1
    fi

    for pkg in "${ARTIFACTS_DIR}"/*.nupkg; do
        echo "    Publishing $(basename "$pkg")..."
        dotnet nuget push "$pkg" \
            --api-key "${NUGET_API_KEY}" \
            --source "${NUGET_SOURCE}" \
            --skip-duplicate
    done

    echo ""
    echo ">>> All packages published to ${NUGET_SOURCE}"
fi
