#!/bin/sh
# リバーシのネット対戦検証用ハーネス(サーバ1 + クライアント2)を起動する(N-6/N-7)。
# run-reversi-network.ps1 の sh 版(D-066)。Godot は環境変数 GODOT_BIN を使う。
# 終了は各ポートへ `quit` コマンドを送る(dotnet run --project src/Statee.Cli -- send --command quit --port <N>)。
#
# 使い方: sh tools/run-reversi-network.sh [server_port] [game_port] [client1_port] [client2_port]
set -e
server_port=${1:-9310}
game_port=${2:-9410}
client1_port=${3:-9311}
client2_port=${4:-9312}

if [ -z "$GODOT_BIN" ]; then
  echo "error: 環境変数 GODOT_BIN に Godot .NET 版のパスを設定すること(D-066)" >&2
  exit 1
fi
root=$(cd "$(dirname "$0")/.." && pwd)

echo "=== Reversi.Server 起動 (port=$server_port game-port=$game_port) ==="
(cd "$root" && dotnet run --project "$root/samples/Reversi.Server" -- "--port=$server_port" "--game-port=$game_port") &

echo "=== client1 起動 (port=$client1_port) ==="
"$GODOT_BIN" --headless --path "$root/samples/Reversi.Godot" -- "--port=$client1_port" --game-host=127.0.0.1 "--game-port=$game_port" &

echo "=== client2 起動 (port=$client2_port) ==="
"$GODOT_BIN" --headless --path "$root/samples/Reversi.Godot" -- "--port=$client2_port" --game-host=127.0.0.1 "--game-port=$game_port" &

echo "起動要求を送った。ping で疎通を確認してから使うこと(起動完了は保証しない)。"
echo "server=$server_port client1=$client1_port client2=$client2_port"
