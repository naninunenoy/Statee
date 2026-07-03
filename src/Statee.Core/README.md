# Statee.Core

Statee フレームワークの中心ライブラリ。State / Command / Log の抽象と、
それらを束ねてリクエストを処理する `StateeHost` を提供する。

- **State**: `IStateProvider`(パス単位のスナップショット提供者)。
  `[StateeState]` / `[StateeField]` を付ければ実装は Statee.Generator が生成する
- **Command**: `CommandHandler` デリゲートを名前付きで登録する。
  組み込みコマンドは `state`(path 必須)と `logs`(tail、既定 50)
- **Log**: `LogBuffer`(リングバッファ)+ `BufferLoggerProvider`
  (`Microsoft.Extensions.Logging` 統合)
- レスポンスの payload は TOON 形式(LLM 向けにトークン効率の良い表形式)

Godot に依存しない純 C# ライブラリで、トランスポート(TCP 等)にも依存しない。
テストは `tests/Statee.Core.Tests`。
