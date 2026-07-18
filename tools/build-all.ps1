# 全 slnx のビルドとテストを順に実行する門番(D-046)。
# フレームワーク(src/)を変更したら実行し、各ゲームが追従できているかを確認する。
# -GamesOnly を付けるとフレームワーク(Statee.slnx)を飛ばす
# (MCP サーバー実行中は Statee.Mcp がロックされてビルドできないため。ゲームのみの変更時に使う)。
param(
    [switch]$GamesOnly
)
$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$slnxs = Get-ChildItem $root -Recurse -Filter *.slnx -Depth 2 | ForEach-Object { $_.FullName }
foreach ($slnx in $slnxs) {
    $isFramework = (Split-Path $slnx -Leaf) -eq "Statee.slnx"
    if ($GamesOnly -and $isFramework) {
        Write-Host "=== skip $slnx (-GamesOnly) ==="
        continue
    }
    Write-Host "=== build $slnx ==="
    dotnet build $slnx
    if ($LASTEXITCODE -ne 0) {
        if ($isFramework) {
            Write-Host "ヒント: Statee.Mcp のビルド失敗は MCP サーバー実行中の exe ロックが典型。" -ForegroundColor Yellow
            Write-Host "       エージェントセッションの MCP サーバーを止めるか、-GamesOnly で再実行する。" -ForegroundColor Yellow
        }
        exit 1
    }
    Write-Host "=== test $slnx ==="
    dotnet test $slnx --no-build
    if ($LASTEXITCODE -ne 0) { exit 1 }
}
Write-Host "all slnx green"
