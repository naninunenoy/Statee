# リバーシのネット対戦検証用ハーネス(サーバ1 + クライアント2)を起動する(N-6/N-7)。
# 8インスタンス級のオーケストレータは作らない(D-051)。3プロセス程度はこのスクリプトで足りる。
#
# 使い方: pwsh tools/run-reversi-network.ps1 [-GodotExe <path>]
# 終了は各ポートへ `quit` コマンドを送る(dotnet run --project src/Statee.Cli -- send --command quit --port <N>)。
param(
    [string]$GodotExe = "C:\Users\naninunenoy\Downloads\Godot_v4.7-stable_mono_win64\Godot_v4.7-stable_mono_win64\Godot_v4.7-stable_mono_win64_console.exe",
    [int]$ServerPort = 9310,
    [int]$GamePort = 9410,
    [int]$Client1Port = 9311,
    [int]$Client2Port = 9312
)
$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent

Write-Host "=== Reversi.Server 起動 (port=$ServerPort game-port=$GamePort) ==="
Start-Process dotnet -ArgumentList "run", "--project", "$root/game/Reversi.Server", "--", "--port=$ServerPort", "--game-port=$GamePort" -WorkingDirectory $root

Write-Host "=== client1 起動 (port=$Client1Port) ==="
Start-Process $GodotExe -ArgumentList "--headless", "--path", "$root/game/Reversi.Godot", "--", "--port=$Client1Port", "--game-host=127.0.0.1", "--game-port=$GamePort"

Write-Host "=== client2 起動 (port=$Client2Port) ==="
Start-Process $GodotExe -ArgumentList "--headless", "--path", "$root/game/Reversi.Godot", "--", "--port=$Client2Port", "--game-host=127.0.0.1", "--game-port=$GamePort"

Write-Host "起動要求を送った。ping で疎通を確認してから使うこと(起動完了は保証しない)。"
Write-Host "server=$ServerPort client1=$Client1Port client2=$Client2Port"
