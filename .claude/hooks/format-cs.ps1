# PostToolUse / afterFileEdit hook: .cs 編集後に dotnet format と CSharpier を実行する
# stdin: Claude Code(PostToolUse: tool_input.file_path) または Cursor(afterFileEdit: file_path)

$raw = [Console]::In.ReadToEnd()
try { $payload = $raw | ConvertFrom-Json } catch { exit 0 }

$filePath = $payload.tool_input.file_path
if ([string]::IsNullOrEmpty($filePath)) { $filePath = $payload.file_path }
if ([string]::IsNullOrEmpty($filePath)) { exit 0 }
if ([IO.Path]::GetExtension($filePath).ToLowerInvariant() -ne '.cs') { exit 0 }

$projectDir = $env:CLAUDE_PROJECT_DIR
if ([string]::IsNullOrEmpty($projectDir) -and $payload.workspace_roots -and $payload.workspace_roots.Count -gt 0) {
    $projectDir = $payload.workspace_roots[0]
}
if ([string]::IsNullOrEmpty($projectDir)) { $projectDir = (Get-Location).Path }
Set-Location $projectDir

$fullPath = [IO.Path]::GetFullPath($filePath)
if (-not (Test-Path -LiteralPath $fullPath)) { exit 0 }
# プロジェクト外(スクラッチパッド等)のファイルは対象外
if (-not $fullPath.StartsWith($projectDir, [StringComparison]::OrdinalIgnoreCase)) { exit 0 }

$relPath = $fullPath.Substring($projectDir.Length).TrimStart('\', '/')
$sln = Join-Path $projectDir 'Statee.slnx'

# dotnet format(.editorconfig ベースのスタイル修正)→ CSharpier(最終的なレイアウト整形)の順。
# 整形は最後に実行した側が勝つため、opinionated な CSharpier を後にする
& dotnet format $sln --include $relPath --verbosity quiet | Out-Null
& dotnet csharpier format $fullPath | Out-Null

exit 0
