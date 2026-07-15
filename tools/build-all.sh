#!/bin/sh
# 全 slnx のビルドとテストを順に実行する門番(D-046)。build-all.ps1 の sh 版(D-066)。
# フレームワーク(src/)を変更したら実行し、各ゲームが追従できているかを確認する。
# 注意: Statee.Mcp は MCP サーバー実行中はロックされてビルドに失敗する。
#       失敗したら MCP サーバー(エージェントのセッション)を疑うこと。
set -e
root=$(cd "$(dirname "$0")/.." && pwd)

find "$root" -maxdepth 3 -name '*.slnx' | while read -r slnx; do
  echo "=== build $slnx ==="
  dotnet build "$slnx"
  echo "=== test $slnx ==="
  dotnet test "$slnx" --no-build
done
echo "all slnx green"
