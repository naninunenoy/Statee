# Statee.Mcp

AI Agent(Claude Code 等)に Statee を公開する汎用 MCP サーバー(stdio)。
ツールは `statee_cli` の 1 つだけで、受け取った引数をそのまま
Statee.Cli(環境変数 `STATEE_CLI` で指定した実行ファイルまたは dll)に渡して実行する。
`.dll` を指定すると `dotnet` 経由で起動するため、Windows / macOS で同じ設定が使える(D-066)。

リポジトリルートの `.mcp.json`(gitignore 済み。環境ごとに作成する)で登録する:

```json
{
  "mcpServers": {
    "statee": {
      "command": "dotnet",
      "args": ["src/Statee.Mcp/bin/Debug/net10.0/Statee.Mcp.dll"],
      "env": {
        "STATEE_CLI": "src/Statee.Cli/bin/Debug/net10.0/Statee.Cli.dll",
        "STATEE_TRACE": "~/.statee/trace.log"
      }
    }
  }
}
```

ゲーム側の変更は CLI の再ビルドだけで反映されるため、
この MCP サーバーはセッションをまたいで同じバイナリを使い続けられる(docs/adr/D-001.md)。
