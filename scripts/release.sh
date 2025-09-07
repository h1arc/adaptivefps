#!/usr/bin/env zsh
set -euo pipefail

# Simple AdaptiveFPS release script
# Usage: ./scripts/release.sh --set-version 1.0.1.0 [--dry-run]

ROOT_DIR="${0:A:h}/.."
PROJECT_NAME="AdaptiveFPS"
CSPROJ="$ROOT_DIR/$PROJECT_NAME/$PROJECT_NAME.csproj"
CONFIG="Release"

usage() {
  cat <<USAGE
Usage: $(basename "$0") --set-version VERSION [--dry-run]

Examples:
  $(basename "$0") --set-version 1.0.1.0
  $(basename "$0") --set-version 1.0.1.0 --dry-run
USAGE
}

VERSION=""
DRY_RUN=false

while (( $# > 0 )); do
  case "$1" in
    --set-version) shift; VERSION=${1:-};;
    --dry-run) DRY_RUN=true;;
    -h|--help) usage; exit 0;;
    *) echo "Unknown arg: $1" >&2; usage; exit 1;;
  esac
  shift
done

if [[ -z "$VERSION" ]]; then
  echo "Error: --set-version is required" >&2
  usage
  exit 1
fi

# Validate version format (x.y.z.w)
if [[ ! "$VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
  echo "Error: Version must be in format x.y.z.w (e.g., 1.0.1.0)" >&2
  exit 1
fi

echo "Setting version to: $VERSION"

if $DRY_RUN; then
  echo "[DRY RUN] Would set version and create release"
  exit 0
fi

# Update version in csproj
sed -i '' -E "s#<Version>[^<]*</Version>#<Version>$VERSION</Version>#g" "$CSPROJ"

# Build the project
echo "Building project..."
dotnet build "$ROOT_DIR/$PROJECT_NAME.sln" -c "$CONFIG" --nologo

# Check build outputs exist
BUILD_DIR="$ROOT_DIR/$PROJECT_NAME/bin/x64/$CONFIG"
DLL_FILE="$BUILD_DIR/$PROJECT_NAME.dll"
JSON_FILE="$BUILD_DIR/$PROJECT_NAME.json"

if [[ ! -f "$DLL_FILE" ]]; then
  echo "Error: Build output not found: $DLL_FILE" >&2
  exit 1
fi

if [[ ! -f "$JSON_FILE" ]]; then
  echo "Error: Manifest not found: $JSON_FILE" >&2
  exit 1
fi

# Create release zip
RELEASE_DIR="$ROOT_DIR/release"
mkdir -p "$RELEASE_DIR"
ZIP_FILE="$RELEASE_DIR/${PROJECT_NAME}_v${VERSION}.zip"

echo "Creating release zip: $ZIP_FILE"
rm -f "$ZIP_FILE"
(cd "$BUILD_DIR" && zip -9 -q "$ZIP_FILE" "$PROJECT_NAME.dll" "$PROJECT_NAME.json")

# Commit version change
git add "$CSPROJ"
git commit -m "Bump version to $VERSION"

# Create git tag
TAG="v$VERSION"
git tag "$TAG"

# Push changes and tag
git push origin main
git push origin "$TAG"

# Create GitHub release
echo "Creating GitHub release: $TAG"
gh release create "$TAG" "$ZIP_FILE" \
  --title "$PROJECT_NAME $TAG" \
  --notes "Release $PROJECT_NAME version $VERSION" \
  --generate-notes

echo ""
echo "✅ Release complete!"
echo "   Version: $VERSION"
echo "   Tag: $TAG"
echo "   Zip: $ZIP_FILE"
echo "   GitHub: https://github.com/h1arc/adaptivefps/releases/tag/$TAG"
