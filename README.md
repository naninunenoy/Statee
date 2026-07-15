# Statee

AI Agent がゲームの動作確認を自動で行うための汎用フレームワークと、
それを活用したサンプルゲーム(スイカゲーム)の実装です。

ゲームが自身の **State(状態)** / **Command(操作)** / **Log(ログ)** を外部に公開し、
AI Agent がコマンドでゲームを駆動しながら State とログを観測して動作を確認する、
という開発・テストのスタイルを実現します。

## 仕組み

```
AI Agent (Claude 等)
  │  MCP
  ▼
MCP Server(汎用・ゲーム非依存)
  │  プロセス起動
  ▼
CLI クライアント(ゲームごとの操作コマンド)
  │  TCP (localhost)
  ▼
ゲーム(Godot / headless 実行可)
  ├─ Godot 層 … エントリポイント・描画・入力
  └─ 純C#層 … ECS + ゲームロジック + Statee フレームワーク
```

- State は「システム全体 / シーン / 個別オブジェクト」の3粒度で公開されます
- freeze(時間凍結)や「N フレーム進める」などの時間制御コマンドにより、AI が再現性のある検証を行えます
- ロジックは Godot 非依存の純粋な C# で実装され、エンジンなしで高速にユニットテストできます

## 技術スタック

- C# / .NET 10(全レイヤー統一)
- Godot 4.7(**.NET 版**が必要です。標準版は C# を実行できません)
- [Arch](https://github.com/genaray/Arch)(ECS)/ [R3](https://github.com/Cysharp/R3) /
  [VitalRouter](https://github.com/hadashiA/VitalRouter) /
  [ZLogger](https://github.com/Cysharp/ZLogger) /
  [ToonEncoder](https://github.com/Cysharp/ToonEncoder) /
  [UnitGenerator](https://github.com/Cysharp/UnitGenerator) /
  [ConsoleAppFramework](https://github.com/Cysharp/ConsoleAppFramework)
- テスト: xUnit + Shouldly

## リポジトリ構成

```
Statee.slnx
├─ src/        … Statee フレームワーク(Godot 非依存)
├─ game/       … サンプルゲーム(スイカゲーム: 純C#ロジック + Godot プロジェクト)
└─ tests/      … テスト
```

## 開発を始める

Windows / macOS の両方で開発できます。

```sh
dotnet tool restore        # CSharpier 等のローカルツールを復元
dotnet build Statee.slnx
```

Godot の .NET 版バイナリのフルパスを環境変数 `GODOT_BIN` に設定してください
(Windows: `*_console.exe` / macOS: `Godot_mono.app/Contents/MacOS/Godot`)。
詳細な手順は [docs/HANDOVER.md](docs/HANDOVER.md) の「新しい環境のセットアップ」を参照してください。

## ドキュメント

| ファイル | 内容 |
|---|---|
| [docs/HANDOVER.md](docs/HANDOVER.md) | 引き継ぎ文書(思想・判断基準・ハマりどころ。最初に読む) |
| [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) | アーキテクチャ(目的・技術スタック・到達点) |
| [docs/adr/](docs/adr/) | 設計上の決定とトレードオフの記録 |
| [docs/GUIDELINE.md](docs/GUIDELINE.md) | 開発ガイドライン(テスト設計・コーディング規約) |
| [CLAUDE.md](CLAUDE.md) | AI Agent 向けの開発指針 |
