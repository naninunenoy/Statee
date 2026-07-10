# 全 slnx のビルドとテストを順に実行する門番(D-046)。
# フレームワーク(src/)を変更したら実行し、各ゲームが追従できているかを確認する。
# 注意: Statee.Mcp は MCP サーバー実行中はロックされてビルドに失敗する。
#       失敗したら MCP サーバー(エージェントのセッション)を疑うこと。
$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$slnxs = Get-ChildItem $root -Recurse -Filter *.slnx -Depth 2 | ForEach-Object { $_.FullName }
foreach ($slnx in $slnxs) {
    Write-Host "=== build $slnx ==="
    dotnet build $slnx
    if ($LASTEXITCODE -ne 0) { exit 1 }
    Write-Host "=== test $slnx ==="
    dotnet test $slnx --no-build
    if ($LASTEXITCODE -ne 0) { exit 1 }
}
Write-Host "all slnx green"
