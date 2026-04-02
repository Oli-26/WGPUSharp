#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
DEMO_PROJECT="$PROJECT_ROOT/src/WgpuSharp.Demo"
DIST_DIR="$PROJECT_ROOT/dist"

usage() {
    cat <<'EOF'
Usage: build.sh <target> [options]

Targets:
  web       Publish as a static WASM Blazor web app
  electron  Package as an Electron desktop app
  apk       Package as an Android APK via Capacitor
  all       Build all targets

Options:
  --aot     Enable ahead-of-time compilation (slower build, faster runtime)
  --release Build release APK (requires signing config in android/keystore.properties)
EOF
    exit 1
}

AOT=false
RELEASE=false
GAME_MODE=false
SCENE_FILE=""
TARGET=""

while [[ $# -gt 0 ]]; do
    case "$1" in
        web|electron|apk|all) TARGET="$1" ;;
        --aot) AOT=true ;;
        --release) RELEASE=true ;;
        --game) GAME_MODE=true ;;
        --scene) shift; SCENE_FILE="$1" ;;
        *) usage ;;
    esac
    shift
done

[[ -z "$TARGET" ]] && usage

check_prereqs() {
    local target="$1"
    command -v dotnet >/dev/null 2>&1 || { echo "Error: .NET SDK not found. Install from https://dot.net"; exit 1; }

    if [[ "$target" == "electron" || "$target" == "apk" || "$target" == "all" ]]; then
        command -v node >/dev/null 2>&1 || { echo "Error: Node.js not found"; exit 1; }
        command -v npm >/dev/null 2>&1  || { echo "Error: npm not found"; exit 1; }
    fi

    if [[ "$target" == "apk" || "$target" == "all" ]]; then
        if [[ -z "${ANDROID_HOME:-}" && -z "${ANDROID_SDK_ROOT:-}" ]]; then
            echo "Warning: ANDROID_HOME / ANDROID_SDK_ROOT not set — Gradle may fail"
        fi
    fi
}

# ── Shared: publish Blazor WASM ────────────────────────────────────────
publish_blazor() {
    echo "==> Publishing Blazor WASM app..."
    local args=(-c Release -o "$DIST_DIR/publish")

    if $AOT; then
        echo "    AOT compilation enabled — this will take a while..."
        args+=(-p:RunAOTCompilation=true)
    fi

    dotnet publish "$DEMO_PROJECT" "${args[@]}"
    echo "    Published to $DIST_DIR/publish/wwwroot"
}

# ── Patch output for game-only mode ───────────────────────────────────
patch_game_mode() {
    local www_dir="$1"
    local scene_name="game.json"

    # Copy scene file into the output if provided
    if [[ -n "$SCENE_FILE" && -f "$SCENE_FILE" ]]; then
        mkdir -p "$www_dir/scenes"
        cp "$SCENE_FILE" "$www_dir/scenes/$scene_name"
        echo "    Embedded scene: $SCENE_FILE -> scenes/$scene_name"
    fi

    # Patch index.html to redirect to the game page (only from root path)
    if [[ -f "$www_dir/index.html" ]]; then
        local redirect_script="<script>if(!location.search.includes('scene='))location.replace(location.origin+'/game?scene=$scene_name');<\/script>"
        sed -i "s#</head>#${redirect_script}</head>#" "$www_dir/index.html"
        echo "    Patched index.html to launch game runtime"
    fi
}

# ── Target: web ────────────────────────────────────────────────────────
build_web() {
    publish_blazor

    local out="$DIST_DIR/web"
    rm -rf "$out"
    cp -r "$DIST_DIR/publish/wwwroot" "$out"

    if $GAME_MODE; then
        patch_game_mode "$out"
    fi

    echo ""
    echo "==> Web build complete: $out"
    echo "    Deploy the contents of that directory to any static host."
    echo "    Local preview: npx serve \"$out\""
}

# ── Target: electron ───────────────────────────────────────────────────
build_electron() {
    publish_blazor

    local electron_dir="$SCRIPT_DIR/electron"
    local www_dir="$electron_dir/www"

    rm -rf "$www_dir"
    cp -r "$DIST_DIR/publish/wwwroot" "$www_dir"

    if $GAME_MODE; then
        patch_game_mode "$www_dir"
    fi

    echo "==> Installing Electron dependencies..."
    cd "$electron_dir"
    npm install

    echo "==> Packaging Electron app..."
    npx electron-builder

    echo ""
    echo "==> Electron build complete. Artifacts in: $electron_dir/out"
}

# ── Target: apk ────────────────────────────────────────────────────────
build_apk() {
    publish_blazor

    local android_dir="$SCRIPT_DIR/android"
    local www_dir="$android_dir/www"

    rm -rf "$www_dir"
    cp -r "$DIST_DIR/publish/wwwroot" "$www_dir"

    if $GAME_MODE; then
        patch_game_mode "$www_dir"
    fi

    echo "==> Installing Capacitor dependencies..."
    cd "$android_dir"
    npm install

    # First-time setup: create the Android project
    if [[ ! -d "$android_dir/android" ]]; then
        echo "==> Initializing Android project (first run)..."
        npx cap add android
    fi

    echo "==> Syncing web assets to Android project..."
    npx cap sync android

    local gradle_task="assembleDebug"
    if $RELEASE; then
        gradle_task="assembleRelease"
    fi

    echo "==> Building APK ($gradle_task)..."
    cd "$android_dir/android"
    ./gradlew "$gradle_task"

    local apk
    if $RELEASE; then
        apk=$(find . -name "*.apk" -path "*/release/*" 2>/dev/null | head -1)
    else
        apk=$(find . -name "*.apk" -path "*/debug/*" 2>/dev/null | head -1)
    fi

    echo ""
    echo "==> APK build complete: $android_dir/android/$apk"
}

# ── Run ────────────────────────────────────────────────────────────────
check_prereqs "$TARGET"

case "$TARGET" in
    web)      build_web ;;
    electron) build_electron ;;
    apk)      build_apk ;;
    all)      build_web; build_electron; build_apk ;;
esac
