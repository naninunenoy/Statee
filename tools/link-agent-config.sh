#!/bin/sh
# Claude Code / Cursor 向け AI 設定のシンボリックリンクを張る(D-081)。
# 正本は .claude/。hook 定義は .cursor/hooks.json が .claude/hooks/ を直接参照する。
# clone 直後や Windows で symlink が欠けているときに実行する。
set -e
root=$(cd "$(dirname "$0")/.." && pwd)
cd "$root"

link_path() {
  src=$1
  dst=$2
  if [ -e "$dst" ] && [ ! -L "$dst" ]; then
    echo "skip $dst (exists and is not a symlink)" >&2
    return 0
  fi
  mkdir -p "$(dirname "$dst")"
  ln -snf "$src" "$dst"
  echo "linked $dst -> $src"
}

link_path "../.claude/skills" ".cursor/skills"
link_path "../.claude/agents" ".cursor/agents"
link_path "CLAUDE.md" "AGENTS.md"

echo "agent config links ready (.claude/ is canonical)"
