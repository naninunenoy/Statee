#!/bin/sh
# C# Godot プロジェクトを godot-dn2cpp で Web エクスポートする(D-082)。
# 0環境では GitHub Releases からエディタと Web テンプレートを取得する。
# GODOT_BIN(公式 .NET 版)は使わない。ABI が違う。
set -e
root=$(cd "$(dirname "$0")/.." && pwd)
pin="$root/tools/godot-dn2cpp.pin"

pin_val() {
  awk -F= -v k="$1" '$1 == k { print $2; exit }' "$pin"
}

REPO=$(pin_val REPO)
VERSION=$(pin_val VERSION)
FORK_TAG=$(pin_val FORK_TAG)
FORK_COMMIT=$(pin_val FORK_COMMIT)
DN2CPP_REPO=$(pin_val DN2CPP_REPO)
DN2CPP_COMMIT=$(pin_val DN2CPP_COMMIT)
GODOT_BASE_COMMIT=$(pin_val GODOT_BASE_COMMIT)
REPO=${GODOT_DN2CPP_RELEASE_REPO:-$REPO}
VERSION=${GODOT_DN2CPP_VERSION:-$VERSION}

mode=export
path=""
out=""
serve=0
force_preset=0

usage() {
  cat <<EOF
usage:
  tools/export-web.sh --path <Godotプロジェクト> [--out <dir>] [--serve] [--force-preset]
  tools/export-web.sh --fetch-only
  tools/export-web.sh --fetch-template-only
  tools/export-web.sh --print-urls
  tools/export-web.sh --print-build-fork

0環境では GitHub Releases($REPO $VERSION)から資材を取得し、
GODOT_DN2CPP_BIN / GODOT_DN2CPP_WEB_TEMPLATE が未設定ならキャッシュへ展開する。
既に両変数が有効なファイルを指していれば取得を省略する。

プレビルドの無いホスト(Linux 等)は --print-build-fork の手順で
フォークを自前ビルドし、GODOT_DN2CPP_BIN を設定する。

環境変数:
  GODOT_DN2CPP_BIN            フォークエディタの実行ファイル
  GODOT_DN2CPP_WEB_TEMPLATE   同じリリースの web-template.zip(展開しない)
  GODOT_DN2CPP_CACHE          取得先(既定: ~/.cache/godot-dn2cpp または %LOCALAPPDATA%)
  GODOT_DN2CPP_VERSION        ピンの上書き
  GODOT_DN2CPP_RELEASE_REPO   リポジトリの上書き(owner/name)
  DN2CPP_WORKSPACE            自前ビルドの clone 先(既定: \$HOME/src)
EOF
}

while [ $# -gt 0 ]; do
  case "$1" in
    --path) path=$2; shift 2 ;;
    --out) out=$2; shift 2 ;;
    --serve) serve=1; shift ;;
    --force-preset) force_preset=1; shift ;;
    --fetch-only) mode=fetch; shift ;;
    --fetch-template-only) mode=fetch-template; shift ;;
    --print-urls) mode=print-urls; shift ;;
    --print-build-fork) mode=print-build-fork; shift ;;
    -h|--help) usage; exit 0 ;;
    *) echo "error: unknown arg: $1" >&2; usage >&2; exit 2 ;;
  esac
done

release_base() {
  printf 'https://github.com/%s/releases/download/%s' "$REPO" "$VERSION"
}

editor_asset_name() {
  case "$1" in
    macos-arm64) printf 'Godot-%s-macos-arm64.zip' "$VERSION" ;;
    windows-x86_64) printf 'Godot-%s-windows-x86_64.zip' "$VERSION" ;;
    *) return 1 ;;
  esac
}

template_asset_name() {
  printf 'godot-%s-web-template.zip' "$VERSION"
}

detect_host() {
  sys=$(uname -s)
  mach=$(uname -m)
  case "$sys" in
    Darwin)
      if [ "$mach" = "arm64" ]; then
        echo macos-arm64
      else
        echo macos-unsupported
      fi
      ;;
    Linux)
      echo linux
      ;;
    MINGW*|MSYS*|CYGWIN*)
      echo windows-x86_64
      ;;
    *)
      echo unknown
      ;;
  esac
}

cache_dir() {
  if [ -n "$GODOT_DN2CPP_CACHE" ]; then
    printf '%s/%s' "$GODOT_DN2CPP_CACHE" "$VERSION"
    return
  fi
  if [ -n "$LOCALAPPDATA" ]; then
    printf '%s/godot-dn2cpp/%s' "$LOCALAPPDATA" "$VERSION"
    return
  fi
  printf '%s/godot-dn2cpp/%s' "${XDG_CACHE_HOME:-$HOME/.cache}" "$VERSION"
}

sha256_of() {
  if command -v sha256sum >/dev/null 2>&1; then
    sha256sum "$1" | awk '{print $1}'
  elif command -v shasum >/dev/null 2>&1; then
    shasum -a 256 "$1" | awk '{print $1}'
  else
    echo "error: sha256sum も shasum も無い" >&2
    exit 1
  fi
}

expected_sha256() {
  sums=$1
  file=$2
  awk -v f="$file" '
    $2 == f || $2 == ("*" f) { print $1; found=1 }
    END { if (!found) exit 1 }
  ' "$sums"
}

download() {
  url=$1
  dest=$2
  echo "download $url" >&2
  echo "     -> $dest" >&2
  if command -v curl >/dev/null 2>&1; then
    curl -fL --retry 4 --retry-delay 4 --retry-all-errors -o "$dest" "$url"
  else
    echo "error: curl が必要" >&2
    exit 1
  fi
}

verify_zip_hash() {
  sums=$1
  zip=$2
  base=$(basename "$zip")
  expected=$(expected_sha256 "$sums" "$base") || {
    echo "error: $base のハッシュが SHA256SUMS.txt に無い" >&2
    exit 1
  }
  actual=$(sha256_of "$zip")
  expected_lc=$(printf '%s' "$expected" | tr 'A-F' 'a-f')
  actual_lc=$(printf '%s' "$actual" | tr 'A-F' 'a-f')
  if [ "$expected_lc" != "$actual_lc" ]; then
    echo "error: ハッシュ不一致: $base" >&2
    echo "  expected $expected_lc" >&2
    echo "  actual   $actual_lc" >&2
    exit 1
  fi
  echo "hash ok $base" >&2
}

ensure_sums() {
  cache=$1
  mkdir -p "$cache"
  sums="$cache/SHA256SUMS.txt"
  if [ ! -s "$sums" ]; then
    download "$(release_base)/SHA256SUMS.txt" "$sums"
  fi
  printf '%s\n' "$sums"
}

fetch_template() {
  cache=$1
  name=$(template_asset_name)
  zip="$cache/$name"
  sums=$(ensure_sums "$cache")
  if [ ! -s "$zip" ]; then
    download "$(release_base)/$name" "$zip"
  fi
  verify_zip_hash "$sums" "$zip"
  printf '%s\n' "$zip"
}

find_editor_bin() {
  extract=$1
  host=$2
  case "$host" in
    windows-x86_64)
      found=$(find "$extract" -name 'Godot-dn2cpp.console.exe' -print 2>/dev/null | head -n 1)
      ;;
    macos-arm64)
      found=$(find "$extract" -path '*/Contents/MacOS/Godot' -print 2>/dev/null | head -n 1)
      if [ -z "$found" ]; then
        found=$(find "$extract" -name 'Godot-dn2cpp.app' -print 2>/dev/null | head -n 1)
        if [ -n "$found" ]; then
          found="$found/Contents/MacOS/Godot"
        fi
      fi
      ;;
    *)
      found=""
      ;;
  esac
  if [ -z "$found" ] || [ ! -e "$found" ]; then
    echo "error: 展開したエディタの実行ファイルが見つからない: $extract" >&2
    find "$extract" | head -n 40 >&2 || true
    exit 1
  fi
  printf '%s\n' "$found"
}

fetch_editor() {
  cache=$1
  host=$2
  name=$(editor_asset_name "$host") || {
    echo "error: このホスト向けのプレビルドエディタは無い: $host" >&2
    exit 1
  }
  zip="$cache/$name"
  sums=$(ensure_sums "$cache")
  if [ ! -s "$zip" ]; then
    download "$(release_base)/$name" "$zip"
  fi
  verify_zip_hash "$sums" "$zip"
  extract="$cache/editor"
  marker="$extract/.extracted-from"
  if [ ! -f "$marker" ] || [ "$(cat "$marker")" != "$name" ]; then
    rm -rf "$extract"
    mkdir -p "$extract"
    unzip -q "$zip" -d "$extract"
    echo "$name" >"$marker"
  fi
  if [ "$host" = "macos-arm64" ]; then
    app=$(find "$extract" -name 'Godot-dn2cpp.app' -print | head -n 1)
    if [ -n "$app" ] && command -v xattr >/dev/null 2>&1; then
      xattr -dr com.apple.quarantine "$app" || true
    fi
  fi
  find_editor_bin "$extract" "$host"
}


file_ok() {
  [ -n "$1" ] && [ -f "$1" ]
}

print_urls() {
  host=$(detect_host)
  echo "repo=$REPO"
  echo "version=$VERSION"
  echo "host=$host"
  echo "SHA256SUMS=$(release_base)/SHA256SUMS.txt"
  echo "template=$(release_base)/$(template_asset_name)"
  if editor_asset_name "$host" >/dev/null 2>&1; then
    echo "editor=$(release_base)/$(editor_asset_name "$host")"
  else
    echo "editor=(none for host $host)"
  fi
}

print_build_fork() {
  ws=${DN2CPP_WORKSPACE:-$HOME/src}
  cat <<EOF
# フォーク自前ビルド(Linux 等、プレビルドが無いホスト向け。D-082)
# godot-dn2cpp を scons するだけでは足りない。dn2cpp の toolchain を
# エディタへ同梱する gates/setup-godot-fork.sh が正本。
# 所要: 数十分〜数時間。Godot 公式の LinuxBSD ビルド依存 + .NET 10 SDK + Python 3.10+

set -e
ws="$ws"
mkdir -p "\$ws"
cd "\$ws"

if [ ! -e dn2cpp/.git ]; then
  git clone https://github.com/${DN2CPP_REPO}.git dn2cpp
fi
git -C dn2cpp fetch origin
git -C dn2cpp checkout ${DN2CPP_COMMIT}

if [ ! -e godot-dn2cpp/.git ]; then
  git clone https://github.com/${REPO}.git godot-dn2cpp
fi
git -C godot-dn2cpp fetch origin --tags
git -C godot-dn2cpp checkout ${FORK_TAG}

cd "\$ws/dn2cpp"
./gates/setup-buildtools.sh
./gates/setup-emsdk.sh
./gates/setup-godot-fork.sh
./gates/setup-godot-fork-web.sh

export GODOT_DN2CPP_BIN="\$(cat "\${DN2CPP_GODOT_FORK_ROOT:-\$HOME/.cache/dn2cpp-godot-fork}/editor.txt")"
export GODOT_DN2CPP_WEB_TEMPLATE="\${DN2CPP_GODOT_FORK_ROOT:-\$HOME/.cache/dn2cpp-godot-fork}/web_template.zip"
# 実行ファイルだけコピーしない。隣の bin/GodotSharp/ が必要。
echo "GODOT_DN2CPP_BIN=\$GODOT_DN2CPP_BIN"
echo "GODOT_DN2CPP_WEB_TEMPLATE=\$GODOT_DN2CPP_WEB_TEMPLATE"
# ピン: fork=$FORK_COMMIT dn2cpp=$DN2CPP_COMMIT base=$GODOT_BASE_COMMIT version=$VERSION
EOF
}

no_prebuilt_editor() {
  host=$1
  echo "error: このホスト($host)向けのプレビルドエディタは無い。" >&2
  echo "  Windows x86_64 / macOS Apple Silicon なら --fetch-only で Releases から取得する。" >&2
  echo "  それ以外(Linux 等)はフォークを自前ビルドして GODOT_DN2CPP_BIN を設定する:" >&2
  echo "    tools/export-web.sh --print-build-fork" >&2
  echo "  手順の本文: .claude/skills/export-web/SKILL.md 「フォーク自前ビルド」" >&2
  echo "  リリース: https://github.com/$REPO/releases/tag/$VERSION" >&2
  exit 1
}

write_preset() {
  target=$1
  template_zip=$2
  export_html=$3
  cfg="$target/export_presets.cfg"
  if [ -f "$cfg" ] && ! grep -q '^; generated-by: tools/export-web' "$cfg"; then
    if [ "$force_preset" -ne 1 ]; then
      echo "error: $cfg は手書きの可能性がある。--force-preset で上書きするか退避すること" >&2
      exit 1
    fi
  fi
  template_abs=$(cd "$(dirname "$template_zip")" && pwd)/$(basename "$template_zip")
  html_abs=$(cd "$(dirname "$export_html")" && pwd)/$(basename "$export_html")
  template_abs=$(printf '%s' "$template_abs" | sed 's|\\|/|g')
  html_abs=$(printf '%s' "$html_abs" | sed 's|\\|/|g')
  cat >"$cfg" <<EOF
; generated-by: tools/export-web
[preset.0]

name="Web"
platform="Web"
runnable=true
dedicated_server=false
custom_features=""
export_filter="all_resources"
include_filter=""
exclude_filter=""
export_path="$html_abs"
encryption_include_filters=""
encryption_exclude_filters=""
encrypt_pck=false
encrypt_directory=false
script_export_mode=2

[preset.0.options]

custom_template/debug=""
custom_template/release="$template_abs"
variant/extensions_support=true
variant/thread_support=false
vram_texture_compression/for_desktop=true
vram_texture_compression/for_mobile=false
html/export_icon=true
html/custom_html_shell=""
html/head_include=""
html/canvas_resize_policy=2
html/focus_canvas_on_start=true
html/experimental_virtual_keyboard=false
progressive_web_app/enabled=false
dotnet/include_scripts_content=false
dotnet/include_debug_symbols=false
dotnet/embed_build_outputs=false
dotnet/export_backend=2
EOF
}

latest_export_log() {
  target=$1
  find "$target" -type f -name 'export-*.log' -path '*/dn2cpp/logs/*' 2>/dev/null | sort | tail -n 1
}

check_success() {
  out_dir=$1
  target=$2
  html="$out_dir/index.html"
  if [ ! -s "$html" ]; then
    echo "error: index.html が無い/空: $html" >&2
    return 1
  fi
  wasm_count=$(find "$out_dir" -name '*.wasm' | wc -l | tr -d ' ')
  pck_count=$(find "$out_dir" -name '*.pck' | wc -l | tr -d ' ')
  if [ "$wasm_count" -lt 1 ] || [ "$pck_count" -lt 1 ]; then
    echo "error: wasm/pck が揃っていない: $out_dir (wasm=$wasm_count pck=$pck_count)" >&2
    return 1
  fi
  log=$(latest_export_log "$target")
  if [ -z "$log" ]; then
    echo "error: dn2cpp の export-*.log が無い。公式 GODOT_BIN を使っていないか確認" >&2
    return 1
  fi
  if grep -q 'missing tools it cannot build without' "$log"; then
    echo "error: dn2cpp がツール不足で失敗した。ログ: $log" >&2
    tail -n 40 "$log" >&2 || true
    return 1
  fi
  if grep -Eqi 'NotSupportedException|transpile failed|CMake Error|emcc: error' "$log"; then
    echo "error: dn2cpp エクスポートが失敗した。ログ: $log" >&2
    tail -n 80 "$log" >&2 || true
    return 1
  fi
  echo "log $log"
  echo "out $out_dir"
  echo "html $html"
}

resolve_bin_and_template() {
  host=$1
  cache=$(cache_dir)
  mkdir -p "$cache"

  if ! file_ok "$GODOT_DN2CPP_BIN"; then
    case "$host" in
      macos-arm64|windows-x86_64) ;;
      *)
        no_prebuilt_editor "$host"
        ;;
    esac
  fi

  if file_ok "$GODOT_DN2CPP_WEB_TEMPLATE"; then
    case "$GODOT_DN2CPP_WEB_TEMPLATE" in
      *.zip) template=$GODOT_DN2CPP_WEB_TEMPLATE ;;
      *)
        echo "error: GODOT_DN2CPP_WEB_TEMPLATE は zip のまま指す(展開しない): $GODOT_DN2CPP_WEB_TEMPLATE" >&2
        exit 1
        ;;
    esac
  else
    template=$(fetch_template "$cache")
  fi

  if file_ok "$GODOT_DN2CPP_BIN"; then
    bin=$GODOT_DN2CPP_BIN
  else
    bin=$(fetch_editor "$cache" "$host")
  fi

  GODOT_DN2CPP_BIN=$bin
  GODOT_DN2CPP_WEB_TEMPLATE=$template
  export GODOT_DN2CPP_BIN GODOT_DN2CPP_WEB_TEMPLATE
  echo "GODOT_DN2CPP_BIN=$GODOT_DN2CPP_BIN"
  echo "GODOT_DN2CPP_WEB_TEMPLATE=$GODOT_DN2CPP_WEB_TEMPLATE"
}

need_dotnet() {
  if ! command -v dotnet >/dev/null 2>&1; then
    echo "error: .NET SDK が無い。net10.0 を入れること" >&2
    exit 1
  fi
  if ! dotnet --list-sdks | grep -q '^10\.'; then
    echo "error: .NET 10 SDK が必要。dotnet --list-sdks に 10.0.x が無い" >&2
    dotnet --list-sdks >&2 || true
    exit 1
  fi
}

host=$(detect_host)

if [ "$mode" = "print-urls" ]; then
  print_urls
  exit 0
fi

if [ "$mode" = "print-build-fork" ]; then
  print_build_fork
  exit 0
fi

if [ "$mode" = "fetch-template" ]; then
  zip=$(fetch_template "$(cache_dir)")
  echo "GODOT_DN2CPP_WEB_TEMPLATE=$zip"
  exit 0
fi

if [ "$mode" = "fetch" ]; then
  resolve_bin_and_template "$host"
  exit 0
fi

if [ -z "$path" ]; then
  echo "error: --path が必要" >&2
  usage >&2
  exit 2
fi

case "$path" in
  /*) target=$path ;;
  *) target="$root/$path" ;;
esac
if [ ! -f "$target/project.godot" ]; then
  echo "error: project.godot が無い: $target" >&2
  exit 1
fi

# エディタが無いホストでは SDK チェックより先に落とす(0環境の Linux で誤解を減らす)
if ! file_ok "$GODOT_DN2CPP_BIN"; then
  case "$host" in
    macos-arm64|windows-x86_64) ;;
    *)
      no_prebuilt_editor "$host"
      ;;
  esac
fi

need_dotnet
resolve_bin_and_template "$host"

name=$(basename "$target")
if [ -z "$out" ]; then
  out="$root/artifacts/web/$name"
else
  case "$out" in
    /*) ;;
    *) out="$root/$out" ;;
  esac
fi
mkdir -p "$out"
html="$out/index.html"

write_preset "$target" "$GODOT_DN2CPP_WEB_TEMPLATE" "$html"

echo "import $target"
"$GODOT_DN2CPP_BIN" --headless --path "$target" --import || true

echo "export-release Web -> $html"
set +e
"$GODOT_DN2CPP_BIN" --headless --path "$target" --export-release "Web" "$html"
ec=$?
set -e
echo "godot exit $ec (成否はログと成果物で判定する)"

check_success "$out" "$target"

echo "serve: python3 -m http.server 8060 --directory $out"
if [ "$serve" -eq 1 ]; then
  if command -v python3 >/dev/null 2>&1; then
    python3 -m http.server 8060 --directory "$out"
  else
    python -m http.server 8060 --directory "$out"
  fi
fi
