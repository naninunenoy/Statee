# Claude Code / Cursor 向け AI 設定のシンボリックリンクを張る(D-081)。
# 正本は .claude/。hook 定義は .cursor/hooks.json が .claude/hooks/ を直接参照する。
# clone 直後や Windows で symlink が欠けているときに実行する。
# Windows では開発者モードまたは管理者権限が symlink 作成に必要。
# 権限が無い場合は .cursor/skills / .cursor/agents / AGENTS.md だけ未作成になるが、
# Cursor は .claude/skills/ と .claude/agents/ を直接読むため hook 以外は動作する。
$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
Set-Location $root

function Ensure-Link {
    param(
        [Parameter(Mandatory = $true)][string]$Target,
        [Parameter(Mandatory = $true)][string]$Link
    )
    $targetPath = if ([IO.Path]::IsPathRooted($Target)) {
        $Target
    } else {
        (Resolve-Path -LiteralPath $Target).Path
    }
    $linkPath = Join-Path $root $Link
    $linkDir = Split-Path $linkPath -Parent
    if ($linkDir -and -not (Test-Path $linkDir)) {
        New-Item -ItemType Directory -Path $linkDir -Force | Out-Null
    }
    if (Test-Path $linkPath) {
        $item = Get-Item -LiteralPath $linkPath -Force
        if (-not $item.LinkType) {
            Write-Host "skip $Link (exists and is not a symlink)"
            return
        }
        Remove-Item -LiteralPath $linkPath -Force
    }
    New-Item -ItemType SymbolicLink -Path $linkPath -Target $targetPath -Force | Out-Null
    Write-Host "linked $Link -> $Target"
}

$failed = $false
foreach ($pair in @(
        @{ Target = ".claude/skills"; Link = ".cursor/skills" },
        @{ Target = ".claude/agents"; Link = ".cursor/agents" },
        @{ Target = "CLAUDE.md"; Link = "AGENTS.md" }
    )) {
    try {
        Ensure-Link -Target $pair.Target -Link $pair.Link
    } catch {
        Write-Warning "$($pair.Link): $($_.Exception.Message)"
        $failed = $true
    }
}

if ($failed) {
    Write-Host ""
    Write-Host "Some links were skipped. Cursor still reads .claude/skills/ and .claude/agents/ directly."
    Write-Host "Re-run as Administrator or enable Windows Developer Mode to create all symlinks."
    exit 1
}

Write-Host "agent config links ready (.claude/ is canonical)"
