# PingTarget.Godot

フレームワーク検証用の最小ダミーターゲット(docs/MEMO.md D-013)。
StateeHost を組み込んだ Godot プロジェクトで、ping / state / logs / quit に応答する。

起動(Godot は .NET 版を使うこと):

```
<godot_console.exe> --headless --path sandbox/PingTarget.Godot -- --port=9310
```

- 初回のみ `--import` が必要(完了後にクラッシュするので exit code は無視: D-016)
- 公開 State: `system/platform`(起動時に確定する不変情報)/
  `system/runtime`(Frame・UptimeSeconds。D-019)
- 操作は `src/Statee.Cli` から(例: `dotnet run --project src/Statee.Cli -- ping`)
