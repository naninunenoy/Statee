# Statee.Cli

Statee を組み込んだターゲット(ゲーム)を TCP 越しに操作する汎用 CLI
(ConsoleAppFramework 製)。AI・人間・スクリプトの共通の操作口。

```
dotnet run --project src/Statee.Cli -- ping --message hello
dotnet run --project src/Statee.Cli -- state --path system/runtime
dotnet run --project src/Statee.Cli -- logs --tail 20
dotnet run --project src/Statee.Cli -- send --command <名前> --arg key=value
dotnet run --project src/Statee.Cli -- quit
```

- 成功: payload(TOON)を stdout に出力して exit 0。失敗: stderr に理由を出力して exit 1
- 接続先は `--port`(既定 9310)
- 環境変数 `STATEE_TRACE` にファイルパスを設定すると、ワイヤ入出力を追記する
  (docs/MEMO.md D-021。`~` はユーザープロファイルに展開)
