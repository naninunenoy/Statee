# C# Godot プロジェクトを godot-dn2cpp で Web エクスポートする(D-082)。
# 0環境では GitHub Releases からエディタと Web テンプレートを取得する。
# GODOT_BIN(公式 .NET 版)は使わない。ABI が違う。
# tools/export-web.sh の PowerShell 版(D-066)。
[CmdletBinding()]
param(
    [string]$Path,
    [string]$Out,
    [switch]$Serve,
    [switch]$ForcePreset,
    [switch]$FetchOnly,
    [switch]$FetchTemplateOnly,
    [switch]$PrintUrls
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$pinPath = Join-Path $root "tools/godot-dn2cpp.pin"

function Read-Pin {
    $repo = $null
    $version = $null
    Get-Content -LiteralPath $pinPath | ForEach-Object {
        if ($_ -match '^REPO=(.+)$') { $script:repoFromPin = $Matches[1] }
        if ($_ -match '^VERSION=(.+)$') { $script:versionFromPin = $Matches[1] }
    }
    $script:Repo = if ($env:GODOT_DN2CPP_RELEASE_REPO) { $env:GODOT_DN2CPP_RELEASE_REPO } else { $script:repoFromPin }
    $script:Version = if ($env:GODOT_DN2CPP_VERSION) { $env:GODOT_DN2CPP_VERSION } else { $script:versionFromPin }
}

function Release-Base {
    "https://github.com/$($script:Repo)/releases/download/$($script:Version)"
}

function Template-AssetName {
    "godot-$($script:Version)-web-template.zip"
}

function Editor-AssetName([string]$HostId) {
    switch ($HostId) {
        "macos-arm64" { "Godot-$($script:Version)-macos-arm64.zip" }
        "windows-x86_64" { "Godot-$($script:Version)-windows-x86_64.zip" }
        default { $null }
    }
}

function Detect-Host {
    if ($IsWindows -or $env:OS -eq "Windows_NT") { return "windows-x86_64" }
    $sys = uname -s
    $mach = uname -m
    if ($sys -eq "Darwin" -and $mach -eq "arm64") { return "macos-arm64" }
    if ($sys -eq "Darwin") { return "macos-unsupported" }
    if ($sys -eq "Linux") { return "linux" }
    return "unknown"
}

function Cache-Dir {
    if ($env:GODOT_DN2CPP_CACHE) {
        return (Join-Path $env:GODOT_DN2CPP_CACHE $script:Version)
    }
    if ($env:LOCALAPPDATA) {
        return (Join-Path $env:LOCALAPPDATA "godot-dn2cpp/$($script:Version)")
    }
    $xdg = if ($env:XDG_CACHE_HOME) { $env:XDG_CACHE_HOME } else { Join-Path $HOME ".cache" }
    return (Join-Path $xdg "godot-dn2cpp/$($script:Version)")
}

function Sha256-Of([string]$File) {
    (Get-FileHash -LiteralPath $File -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Expected-Sha256([string]$Sums, [string]$FileName) {
    foreach ($line in Get-Content -LiteralPath $Sums) {
        $parts = $line -split '\s+', 2
        if ($parts.Count -lt 2) { continue }
        $name = $parts[1].TrimStart('*')
        if ($name -eq $FileName) { return $parts[0].ToLowerInvariant() }
    }
    throw "error: $FileName のハッシュが SHA256SUMS.txt に無い"
}

function Download-File([string]$Url, [string]$Dest) {
    Write-Host "download $Url"
    Write-Host "     -> $Dest"
    $dir = Split-Path $Dest -Parent
    if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir | Out-Null }
    Invoke-WebRequest -UseBasicParsing -Uri $Url -OutFile $Dest
}

function Verify-ZipHash([string]$Sums, [string]$Zip) {
    $base = [IO.Path]::GetFileName($Zip)
    $expected = Expected-Sha256 $Sums $base
    $actual = Sha256-Of $Zip
    if ($expected -ne $actual) {
        throw "error: ハッシュ不一致: $base`n  expected $expected`n  actual   $actual"
    }
    Write-Host "hash ok $base"
}

function Ensure-Sums([string]$Cache) {
    if (-not (Test-Path $Cache)) { New-Item -ItemType Directory -Path $Cache | Out-Null }
    $sums = Join-Path $Cache "SHA256SUMS.txt"
    if (-not (Test-Path $sums) -or (Get-Item $sums).Length -eq 0) {
        Download-File "$(Release-Base)/SHA256SUMS.txt" $sums
    }
    return $sums
}

function Fetch-Template([string]$Cache) {
    $name = Template-AssetName
    $zip = Join-Path $Cache $name
    $sums = Ensure-Sums $Cache
    if (-not (Test-Path $zip) -or (Get-Item $zip).Length -eq 0) {
        Download-File "$(Release-Base)/$name" $zip
    }
    Verify-ZipHash $sums $zip
    return $zip
}

function Find-EditorBin([string]$Extract, [string]$HostId) {
    if ($HostId -eq "windows-x86_64") {
        $found = Get-ChildItem -LiteralPath $Extract -Recurse -Filter "Godot-dn2cpp.console.exe" | Select-Object -First 1
        if ($found) { return $found.FullName }
    }
    if ($HostId -eq "macos-arm64") {
        $found = Get-ChildItem -LiteralPath $Extract -Recurse -Filter "Godot" | Where-Object {
            $_.FullName -like "*Contents/MacOS/Godot" -or $_.FullName -like "*Contents\MacOS\Godot"
        } | Select-Object -First 1
        if ($found) { return $found.FullName }
    }
    throw "error: 展開したエディタの実行ファイルが見つからない: $Extract"
}

function Fetch-Editor([string]$Cache, [string]$HostId) {
    $name = Editor-AssetName $HostId
    if (-not $name) { throw "error: このホスト向けのプレビルドエディタは無い: $HostId" }
    $zip = Join-Path $Cache $name
    $sums = Ensure-Sums $Cache
    if (-not (Test-Path $zip) -or (Get-Item $zip).Length -eq 0) {
        Download-File "$(Release-Base)/$name" $zip
    }
    Verify-ZipHash $sums $zip
    $extract = Join-Path $Cache "editor"
    $marker = Join-Path $extract ".extracted-from"
    $needExtract = -not (Test-Path $marker) -or ((Get-Content $marker -Raw).Trim() -ne $name)
    if ($needExtract) {
        if (Test-Path $extract) { Remove-Item -Recurse -Force $extract }
        New-Item -ItemType Directory -Path $extract | Out-Null
        Unblock-File -LiteralPath $zip -ErrorAction SilentlyContinue
        Expand-Archive -LiteralPath $zip -DestinationPath $extract -Force
        Set-Content -LiteralPath $marker -Value $name
    }
    return (Find-EditorBin $extract $HostId)
}

function File-Ok([string]$Value) {
    return ($Value -and (Test-Path -LiteralPath $Value -PathType Leaf))
}

function Print-Urls([string]$HostId) {
    Write-Output "repo=$($script:Repo)"
    Write-Output "version=$($script:Version)"
    Write-Output "host=$HostId"
    Write-Output "SHA256SUMS=$(Release-Base)/SHA256SUMS.txt"
    Write-Output "template=$(Release-Base)/$(Template-AssetName)"
    $ed = Editor-AssetName $HostId
    if ($ed) { Write-Output "editor=$(Release-Base)/$ed" } else { Write-Output "editor=(none for host $HostId)" }
}

function Write-Preset([string]$Target, [string]$TemplateZip, [string]$ExportHtml) {
    $cfg = Join-Path $Target "export_presets.cfg"
    if ((Test-Path $cfg) -and -not (Select-String -LiteralPath $cfg -Pattern '^; generated-by: tools/export-web' -Quiet)) {
        if (-not $ForcePreset) {
            throw "error: $cfg は手書きの可能性がある。-ForcePreset で上書きするか退避すること"
        }
    }
    $templateAbs = (Resolve-Path $TemplateZip).Path.Replace('\', '/')
    $htmlDir = Split-Path $ExportHtml -Parent
    if (-not (Test-Path $htmlDir)) { New-Item -ItemType Directory -Path $htmlDir | Out-Null }
    $htmlAbs = ((Join-Path (Resolve-Path $htmlDir).Path ([IO.Path]::GetFileName($ExportHtml)))).Replace('\', '/')
    @"
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
export_path="$htmlAbs"
encryption_include_filters=""
encryption_exclude_filters=""
encrypt_pck=false
encrypt_directory=false
script_export_mode=2

[preset.0.options]

custom_template/debug=""
custom_template/release="$templateAbs"
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
"@ | Set-Content -LiteralPath $cfg -Encoding utf8
}

function Latest-ExportLog([string]$Target) {
    Get-ChildItem -LiteralPath $Target -Recurse -Filter "export-*.log" -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match '[\\/]dn2cpp[\\/]logs[\\/]' } |
        Sort-Object FullName |
        Select-Object -Last 1
}

function Check-Success([string]$OutDir, [string]$Target) {
    $html = Join-Path $OutDir "index.html"
    if (-not (Test-Path $html) -or (Get-Item $html).Length -eq 0) {
        throw "error: index.html が無い/空: $html"
    }
    $wasm = @(Get-ChildItem -LiteralPath $OutDir -Filter "*.wasm").Count
    $pck = @(Get-ChildItem -LiteralPath $OutDir -Filter "*.pck").Count
    if ($wasm -lt 1 -or $pck -lt 1) {
        throw "error: wasm/pck が揃っていない: $OutDir (wasm=$wasm pck=$pck)"
    }
    $log = Latest-ExportLog $Target
    if (-not $log) {
        throw "error: dn2cpp の export-*.log が無い。公式 GODOT_BIN を使っていないか確認"
    }
    $text = Get-Content -LiteralPath $log.FullName -Raw
    if ($text -match 'missing tools it cannot build without') {
        Get-Content $log.FullName -Tail 40 | Write-Host
        throw "error: dn2cpp がツール不足で失敗した。ログ: $($log.FullName)"
    }
    if ($text -match '(?i)NotSupportedException|transpile failed|CMake Error|emcc: error') {
        Get-Content $log.FullName -Tail 80 | Write-Host
        throw "error: dn2cpp エクスポートが失敗した。ログ: $($log.FullName)"
    }
    Write-Host "log $($log.FullName)"
    Write-Host "out $OutDir"
    Write-Host "html $html"
}

function Resolve-BinAndTemplate([string]$HostId) {
    $cache = Cache-Dir
    if (-not (Test-Path $cache)) { New-Item -ItemType Directory -Path $cache | Out-Null }

    if (-not (File-Ok $env:GODOT_DN2CPP_BIN)) {
        if ($HostId -ne "macos-arm64" -and $HostId -ne "windows-x86_64") {
            throw @"
error: このホスト($HostId)向けのプレビルドエディタは無い。
  Windows x86_64 か macOS Apple Silicon で実行するか、
  フォークを自前ビルドして GODOT_DN2CPP_BIN に実行ファイルを設定すること。
  リリース: https://github.com/$($script:Repo)/releases/tag/$($script:Version)
"@
        }
    }

    if (File-Ok $env:GODOT_DN2CPP_WEB_TEMPLATE) {
        if ($env:GODOT_DN2CPP_WEB_TEMPLATE -notlike '*.zip') {
            throw "error: GODOT_DN2CPP_WEB_TEMPLATE は zip のまま指す(展開しない): $($env:GODOT_DN2CPP_WEB_TEMPLATE)"
        }
        $script:Template = $env:GODOT_DN2CPP_WEB_TEMPLATE
    } else {
        $script:Template = Fetch-Template $cache
    }

    if (File-Ok $env:GODOT_DN2CPP_BIN) {
        $script:Bin = $env:GODOT_DN2CPP_BIN
    } else {
        $script:Bin = Fetch-Editor $cache $HostId
    }

    $env:GODOT_DN2CPP_BIN = $script:Bin
    $env:GODOT_DN2CPP_WEB_TEMPLATE = $script:Template
    Write-Host "GODOT_DN2CPP_BIN=$($script:Bin)"
    Write-Host "GODOT_DN2CPP_WEB_TEMPLATE=$($script:Template)"
}

function Need-Dotnet {
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw "error: .NET SDK が無い。net10.0 を入れること"
    }
    $sdks = dotnet --list-sdks
    if ($sdks -notmatch '^10\.') {
        Write-Host $sdks
        throw "error: .NET 10 SDK が必要。dotnet --list-sdks に 10.0.x が無い"
    }
}

Read-Pin
$hostId = Detect-Host

if ($PrintUrls) {
    Print-Urls $hostId
    exit 0
}

if ($FetchTemplateOnly) {
    $zip = Fetch-Template (Cache-Dir)
    Write-Host "GODOT_DN2CPP_WEB_TEMPLATE=$zip"
    exit 0
}

if ($FetchOnly) {
    Resolve-BinAndTemplate $hostId
    exit 0
}

if (-not $Path) {
    throw "error: -Path が必要"
}

$target = if ([IO.Path]::IsPathRooted($Path)) { $Path } else { Join-Path $root $Path }
if (-not (Test-Path (Join-Path $target "project.godot"))) {
    throw "error: project.godot が無い: $target"
}

if (-not (File-Ok $env:GODOT_DN2CPP_BIN)) {
    if ($hostId -ne "macos-arm64" -and $hostId -ne "windows-x86_64") {
        throw @"
error: このホスト($hostId)向けのプレビルドエディタは無い。
  Windows x86_64 か macOS Apple Silicon で実行するか、
  フォークを自前ビルドして GODOT_DN2CPP_BIN に実行ファイルを設定すること。
  リリース: https://github.com/$($script:Repo)/releases/tag/$($script:Version)
"@
    }
}

Need-Dotnet
Resolve-BinAndTemplate $hostId

$name = Split-Path $target -Leaf
if (-not $Out) {
    $Out = Join-Path $root "artifacts/web/$name"
} elseif (-not [IO.Path]::IsPathRooted($Out)) {
    $Out = Join-Path $root $Out
}
if (-not (Test-Path $Out)) { New-Item -ItemType Directory -Path $Out | Out-Null }
$html = Join-Path $Out "index.html"

Write-Preset $target $script:Template $html

Write-Host "import $target"
& $script:Bin --headless --path $target --import
# import のクラッシュは無視(D-016)

Write-Host "export-release Web -> $html"
& $script:Bin --headless --path $target --export-release "Web" $html
Write-Host "godot exit $LASTEXITCODE (成否はログと成果物で判定する)"

Check-Success $Out $target

Write-Host "serve: python -m http.server 8060 --directory $Out"
if ($Serve) {
    $py = Get-Command python3 -ErrorAction SilentlyContinue
    if (-not $py) { $py = Get-Command python -ErrorAction SilentlyContinue }
    if (-not $py) { throw "error: python が無い" }
    & $py.Source -m http.server 8060 --directory $Out
}
