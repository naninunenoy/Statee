#!/bin/sh
# PostToolUse hook: .cs ファイルの編集後に dotnet format と CSharpier を実行する
# macOS / Linux / Windows (Git Bash) 用。Git Bash の無い Windows 環境では format-cs.ps1 を使う
# stdin: フックペイロード JSON(tool_input.file_path を参照)

payload=$(cat)

# tool_input.file_path を抽出(jq があれば jq、無ければ sed でフォールバック)
if command -v jq >/dev/null 2>&1; then
  file_path=$(printf '%s' "$payload" | jq -r '.tool_input.file_path // empty')
else
  file_path=$(printf '%s' "$payload" | sed -n 's/.*"file_path"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p')
fi
[ -n "$file_path" ] || exit 0

# Windows パスを正規化(JSON エスケープ由来の \\ や \ を / に)
file_path=$(printf '%s' "$file_path" | tr '\\' '/' | tr -s '/')

case "$file_path" in
  *.cs) ;;
  *) exit 0 ;;
esac

project_dir="${CLAUDE_PROJECT_DIR:-$(pwd)}"
project_dir=$(printf '%s' "$project_dir" | tr '\\' '/' | tr -s '/')
cd "$project_dir" 2>/dev/null || exit 0
proj=$(pwd)

[ -f "$file_path" ] || exit 0

# プロジェクト外(スクラッチパッド等)のファイルは対象外
file_dir=$(cd "$(dirname "$file_path")" 2>/dev/null && pwd) || exit 0
case "$file_dir" in
  "$proj" | "$proj"/*) ;;
  *) exit 0 ;;
esac

# dotnet format の --include はワークスペースからの相対パスを取る
rel="${file_dir#"$proj"}"
rel="${rel#/}"
base=$(basename "$file_path")
if [ -n "$rel" ]; then rel="$rel/$base"; else rel="$base"; fi

# dotnet format(.editorconfig ベースのスタイル修正)→ CSharpier(最終的なレイアウト整形)の順。
# 整形は後に実行した側が勝つため、opinionated な CSharpier を後にする
dotnet format Statee.slnx --include "$rel" --verbosity quiet >/dev/null 2>&1
dotnet csharpier format "$file_path" >/dev/null 2>&1

exit 0
