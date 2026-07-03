# PingTarget.Godot 開発指針

- **最小のまま保つ**(D-013)。ゲーム的な機能はここに足さない。
  フレームワークの新機能を検証する最低限のコードだけを置く
- **Godot API はメインスレッド以外から触らない**。コマンドハンドラと
  `CaptureState()` はソケットスレッドで走る。不変情報は `_Ready` で一度だけ
  スナップショットし、可変値は Interlocked / Stopwatch 等でスレッド安全に読む。
  メインスレッドが必要な処理は `Callable.From(...).CallDeferred()` に逃がす
- State の公開は `[StateeState]` / `[StateeField]` 宣言を基本とする(D-022)。
  手書き `IStateProvider` は生成で表現できない場合のみ
- Godot.NET.Sdk は ImplicitUsings 無効。System 系の using を明示する。
  NuGet 依存を足したら `CopyLocalLockFileAssemblies` が効いているか確認(D-018 知見)
