#!/usr/bin/env zsh
set -euo pipefail

# AdaptiveFPS release helper
# - Bumps version in csproj (major/minor/patch or --set)
# - Builds Release
# - Packages zip (dll + manifest)
# - Creates GitHub release via gh
# - Emits dist/repo.json (pluginmaster) pointing to the release asset

ROOT_DIR="${0:A:h}/.."
CSProj="$ROOT_DIR/AdaptiveFPS/AdaptiveFPS.csproj"
PROJECT_NAME="AdaptiveFPS"
OWNER="h1arc"
REPO="adaptivefps"
CONFIG="Release"
ARCH_DIR="x64"
DIST_DIR="$ROOT_DIR/dist"

usage() {
  cat <<USAGE
Usage: $(basename "$0") [--major|--minor|--patch|--set VERSION] [--plugin-dir PATH] [--plugin-first] [--push] [--commit-message MSG] [--no-release] [--dry-run]

Defaults: --patch, owner=$OWNER, repo=$REPO

Examples:
  $(basename "$0") --set 1.0.0.0
  $(basename "$0") --minor
  $(basename "$0") --patch --dry-run
  $(basename "$0") --no-release --repo-json-in ../plugin/repo.json --repo-json-out ../plugin/repo.json
  $(basename "$0") --plugin-dir "$ROOT_DIR/../plugin" --plugin-first --push --set 1.0.0.0

Advanced (optional overrides):
  --owner OWNER  --repo REPO       Override GitHub repo used for ASSET_URL and release
  --repo-json-in PATH|URL          Read existing repo.json from file or URL
  --repo-json-out PATH             Write updated repo.json to this path
USAGE
}

MODE="patch"
SET_VERSION=""
DRY_RUN=false
REPO_JSON_IN=""
REPO_JSON_OUT=""
DO_RELEASE=true
PLUGIN_DIR=""
PLUGIN_FIRST=false
DO_PUSH=false
COMMIT_MSG=""

while (( $# > 0 )); do
  case "$1" in
    --major|--minor|--patch) MODE=${1#--};;
    --set) shift; SET_VERSION=${1:-};;
  --owner) shift; OWNER=${1:-};;
  --repo) shift; REPO=${1:-};;
  --repo-json-in) shift; REPO_JSON_IN=${1:-};;
  --repo-json-out) shift; REPO_JSON_OUT=${1:-};;
  --no-release) DO_RELEASE=false;;
  --plugin-dir) shift; PLUGIN_DIR=${1:-};;
  --plugin-first) PLUGIN_FIRST=true;;
  --push) DO_PUSH=true;;
  --commit-message) shift; COMMIT_MSG=${1:-};;
    --dry-run) DRY_RUN=true;;
    -h|--help) usage; exit 0;;
    *) echo "Unknown arg: $1" >&2; usage; exit 1;;
  esac
  shift
done

require_cmd() { command -v "$1" >/dev/null 2>&1 || { echo "Missing required command: $1" >&2; exit 1; }; }
require_cmd dotnet
require_cmd zip
require_cmd jq
if $DO_RELEASE; then require_cmd gh; fi
if $DO_PUSH; then require_cmd git; fi

# Auto-detect OWNER/REPO from git remote if not explicitly set later by flags
detect_owner_repo() {
  local url
  url=$(git -C "$ROOT_DIR" remote get-url origin 2>/dev/null || true)
  [[ -n "$url" ]] || return 0
  # https://github.com/owner/repo(.git) or git@github.com:owner/repo(.git)
  local m
  if [[ "$url" =~ github.com[:/][^/]+/[^/]+ ]]; then
    m=${url##*github.com[:/]}
    m=${m%.git}
    local o r
    o=${m%%/*}
    r=${m#*/}
    [[ -n "$o" && -n "$r" ]] && OWNER="$o" REPO="$r"
  fi
}

current_version() {
  # Extract <Version>X.Y.Z.W</Version> from csproj (first match)
  local v
  v=$(grep -oE '<Version>[0-9]+(\.[0-9]+){0,3}</Version>' "$CSProj" | head -1 | sed -E 's#</?Version>##g')
  [[ -n "$v" ]] || { echo "Unable to read <Version> from $CSProj" >&2; exit 1; }
  echo "$v"
}

pad_version() {
  # Ensure four-part version (fill missing with zeros)
  local v=$1
  local IFS='.' arr; arr=(${=v})
  while (( ${#arr[@]} < 4 )); do arr+=('0'); done
  echo "${(j:.:)arr}"
}

bump_version() {
  local v=$1
  local IFS='.' arr; arr=(${=v})
  while (( ${#arr[@]} < 4 )); do arr+=('0'); done
  local major=${arr[1]} minor=${arr[2]} patch=${arr[3]} rev=${arr[4]}
  case "$MODE" in
    major) major=$((major+1)); minor=0; patch=0; rev=0;;
    minor) minor=$((minor+1)); patch=0; rev=0;;
    patch) patch=$((patch+1)); rev=0;;
  esac
  echo "$major.$minor.$patch.$rev"
}

set_version_in_csproj() {
  local newv=$1
  # Replace first <Version>...</Version>
  gsed -i '' -E "0,/<Version>.*<\/Version>/s//<Version>${newv}<\\/Version>/" "$CSProj" 2>/dev/null \
    || sed -i '' -E "1,/<Version>.*<\/Version>/s//<Version>${newv}<\\/Version>/" "$CSProj"
}

V_OLD=$(current_version)
V_NEW=${SET_VERSION:-$(bump_version "$(pad_version "$V_OLD")")}

echo "Current version: $V_OLD"
echo "New version:     $V_NEW"

if $DRY_RUN; then
  echo "[dry-run] Skipping file edits/build/release"
  exit 0
fi

set_version_in_csproj "$V_NEW"

echo "Building..."
dotnet build "$ROOT_DIR/AdaptiveFPS.sln" -c "$CONFIG" -nologo

OUT_DIR="$ROOT_DIR/AdaptiveFPS/bin/$ARCH_DIR/$CONFIG"
DLL="$OUT_DIR/${PROJECT_NAME}.dll"
MANIFEST="$OUT_DIR/${PROJECT_NAME}.json"
MANIFEST_SRC="$ROOT_DIR/AdaptiveFPS/${PROJECT_NAME}.json"

[[ -f "$DLL" && -f "$MANIFEST" ]] || { echo "Build outputs not found: $DLL or $MANIFEST" >&2; exit 1; }

mkdir -p "$DIST_DIR"
ZIP="$DIST_DIR/${PROJECT_NAME}_v${V_NEW}.zip"
rm -f "$ZIP"
(
  cd "$OUT_DIR"
  zip -9 -q "$ZIP" "${PROJECT_NAME}.dll" "${PROJECT_NAME}.json"
)

TAG="v$V_NEW"
RELEASE_TITLE="$PROJECT_NAME $TAG"
detect_owner_repo
ASSET_URL="https://github.com/${OWNER}/${REPO}/releases/download/${TAG}/${PROJECT_NAME}_v${V_NEW}.zip"

[[ -f "$MANIFEST_SRC" ]] || { echo "Missing manifest: $MANIFEST_SRC" >&2; exit 1; }

NAME=$(jq -r .Name "$MANIFEST_SRC")
INTERNAL=$(jq -r .InternalName "$MANIFEST_SRC")
AUTHOR=$(jq -r .Author "$MANIFEST_SRC")
PUNCHLINE=$(jq -r .Punchline "$MANIFEST_SRC")
DESCRIPTION=$(jq -r .Description "$MANIFEST_SRC")
REPO_URL=$(jq -r .RepoUrl "$MANIFEST_SRC")
TAGS=$(jq -c .Tags "$MANIFEST_SRC")

# Create single plugin block JSON
PLUGIN_JSON=$(jq -n \
  --arg name "$NAME" \
  --arg internalName "$INTERNAL" \
  --arg author "$AUTHOR" \
  --arg punchline "$PUNCHLINE" \
  --arg description "$DESCRIPTION" \
  --arg repoUrl "$REPO_URL" \
  --arg assemblyVersion "$V_NEW" \
  --arg download "$ASSET_URL" \
  --argjson tags "$TAGS" \
  '{name:$name, internalName:$internalName, author:$author, punchline:$punchline, description:$description, repoUrl:$repoUrl, tags:$tags, assemblyVersion:$assemblyVersion, dalamudApiLevel:10, downloadLinkInstall:$download, downloadLinkUpdate:$download}')

# Default plugin dir to ../plugin if present and not provided
if [[ -z "$PLUGIN_DIR" && -d "$ROOT_DIR/../plugin" ]]; then
  PLUGIN_DIR="$ROOT_DIR/../plugin"
fi

# Optionally update plugin repo first
if $PLUGIN_FIRST; then
  update_repo_json
fi

if $DO_RELEASE; then
  echo "Creating GitHub release ${TAG}..."
  gh release create "$TAG" "$ZIP" \
    --title "$RELEASE_TITLE" \
    --notes "Automated release for $PROJECT_NAME $TAG"
else
  echo "[no-release] Skipping GitHub release creation. ASSET_URL will point to the would-be release URL: $ASSET_URL"
fi

# If we didn't update first, update now
if ! $PLUGIN_FIRST; then
  update_repo_json
fi

update_repo_json() {
  # If a plugin directory is specified, target its repo.json by default
  if [[ -n "$PLUGIN_DIR" ]]; then
    REPO_JSON_OUT="${REPO_JSON_OUT:-$PLUGIN_DIR/repo.json}"
    REPO_JSON_IN="${REPO_JSON_IN:-$REPO_JSON_OUT}"
  fi

  [[ -n "$REPO_JSON_OUT" ]] || REPO_JSON_OUT="$DIST_DIR/repo.json"
  mkdir -p "$(dirname "$REPO_JSON_OUT")"

  if [[ -n "$REPO_JSON_IN" ]]; then
    # Upsert into existing repo.json (file or URL) by internalName
    TMP_IN=""
    if [[ "$REPO_JSON_IN" == http://* || "$REPO_JSON_IN" == https://* ]]; then
      require_cmd curl
      TMP_IN=$(mktemp)
      if ! curl -fsSL "$REPO_JSON_IN" -o "$TMP_IN"; then
        echo "Warning: could not fetch $REPO_JSON_IN; creating new repo.json" >&2
        echo '{"version":1,"plugins":[]}' > "$TMP_IN"
      fi
      INPUT_JSON="$TMP_IN"
    else
      if [[ ! -f "$REPO_JSON_IN" ]]; then
        echo '{"version":1,"plugins":[]}' > "$REPO_JSON_OUT"
        INPUT_JSON="$REPO_JSON_OUT"
      else
        INPUT_JSON="$REPO_JSON_IN"
      fi
    fi

    jq \
      --argjson plugin "$PLUGIN_JSON" \
      --arg internalName "$INTERNAL" \
      '
        def normalize:
          if type=="array" then {version:1, plugins:.}
          elif (has("plugins") and (.plugins|type=="array")) then {version:(.version//1), plugins:.plugins}
          elif (has("Plugins") and (.Plugins|type=="array")) then {version:(.version//1), plugins:.Plugins}
          else {version:(.version//1), plugins:[]}
          end;
        normalize
        | .plugins = ([ .plugins[] | select(.internalName != $internalName) ] + [$plugin])
      ' "$INPUT_JSON" > "$REPO_JSON_OUT"

    [[ -n "$TMP_IN" ]] && rm -f "$TMP_IN"
  else
    echo '{"version":1,"plugins":[]}' | jq --argjson plugin "$PLUGIN_JSON" '.plugins += [$plugin]' > "$REPO_JSON_OUT"
  fi

  # Optionally commit/push in plugin repo
  if $DO_PUSH; then
    local repo_dir
    if [[ -n "$PLUGIN_DIR" ]]; then
      repo_dir="$PLUGIN_DIR"
    else
      # infer from output path
      repo_dir="$(cd "$(dirname "$REPO_JSON_OUT")" && pwd)"
    fi
    if [[ -d "$repo_dir/.git" ]]; then
      (
        cd "$repo_dir"
        git add "$(realpath "$REPO_JSON_OUT")" 2>/dev/null || git add "${REPO_JSON_OUT#${repo_dir}/}"
        if ! git diff --cached --quiet -- "${REPO_JSON_OUT#${repo_dir}/}"; then
          local msg
          msg=${COMMIT_MSG:-"Update ${PROJECT_NAME} to v${V_NEW}"}
          git commit -m "$msg"
          local branch
          branch=${PLUGIN_BRANCH:-$(git rev-parse --abbrev-ref HEAD)}
          git push origin "$branch"
        else
          echo "[push] No changes to commit in $repo_dir"
        fi
      )
    else
      echo "[push] Skipping: $repo_dir is not a git repository"
    fi
  fi
}

echo
echo "Done. Artifacts:"
echo "  Zip:       $ZIP"
if $DO_RELEASE; then
  echo "  Release:   $RELEASE_TITLE"
else
  echo "  Release:   (skipped)"
fi
echo "  repo.json: $REPO_JSON_OUT (copy/commit to your plugin repo)"
