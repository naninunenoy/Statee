# Statee 開発計画

## 目的

AI Agent がゲームの動作確認を自動で行うための汎用フレームワーク(Statee)の定義・実装と、
それを活用したサンプルゲーム(スイカゲーム)の実装。

- 本来ゲームに限らない汎用フレームワークだが、最初の目的は Godot でのゲーム開発用途
- 全て C# (.NET 10) で開発する

## 基本コンセプト

対象システム(ゲーム)は以下の3つを外部に公開する。

1. **State** — システムの状態。AI が観測する
2. **Command** — 操作の受け口。AI がコマンドでゲームを駆動する
3. **Log** — 共通ログ。AI が参照して挙動を確認する

### State の粒度

| 粒度 | 例 |
|---|---|
| システム全体 | システム時刻 / フレーム / プラットフォーム情報 / ハードウェア情報 |
| 画面・シーンの内容 | UI の配置、ステージ名、ステージ上のオブジェクト一覧 |
| シーン上の特定オブジェクト | 名前、ID、状態(HP や攻撃力などのステータス) |

## 全体アーキテクチャ

```
AI Agent (Claude 等)
  │  MCP
  ▼
MCP Server(汎用・ゲーム非依存 → 再ビルド不要)
  │  プロセス起動
  ▼
CLI クライアント(ゲーム依存の実装はここ / ConsoleAppFramework)
  │  TCP (localhost)
  ▼
ゲーム(Godot 4.7.stable / --headless 実行前提)
  ├─ Godot 層 … EntryPoint・描画・入力・接続待ち受けの起動
  └─ 純C#層 … ECS(Arch)+ ゲームロジック + Statee フレームワーク
```

- ゲームはコマンドベースで駆動される(外部接続からコマンドを注入)
- State 取得は **pull 型スナップショット** を基本とし、**TOON 形式**でシリアライズ
- コマンド駆動の設計は、将来のネットワーク対戦(リモートからのコマンド注入)への展開を意識する

## 時間制御・決定論

AI による自動確認の再現性を担保する要。

- 即時 pause
- N フレーム進めて pause(step 実行)
- 乱数シードの外部注入
- `godot --headless` での実行を前提とする(CI / AI 検証)

## テスト方針

詳細は **GUIDELINE.md**([unity-coding-skills](https://github.com/nowsprinting/unity-coding-skills)
のエッセンスを本プロジェクト向けに翻案したもの。MEMO.md D-014)。要点:

- 基本理念は「**Green means done**」— エージェントが自律開発できるよう、テストの緑を信頼できる完了シグナルに保つ
- テストはレイヤー化する: 純C#ユニットテスト(最厚)→ headless Godot 統合テスト(薄く)→
  ビジュアル検証 → 手動テスト(自動化できないもののみ)
- **統合テストのハーネスは Statee 自身**(Command 駆動 → State/Log 検証)。ドッグフーディングを兼ねる
- 機能実装はテストファーストの4段階(スケルトン → 失敗するテスト → 実装 → リファクタリング)で進め、
  各段階でコミットする
- ガイドラインから導かれた Statee への機能要件(条件待機コマンド、UI 幾何の State 公開、
  InputEvent 注入、安定 ID、待ち受けのビルド分離)は GUIDELINE.md §7 参照

## 技術スタック(決定済み)

| 領域 | 採用 | 備考 |
|---|---|---|
| 言語 / ランタイム | C# / .NET 10 | |
| ゲームエンジン | Godot 4.7.stable(.NET 版) | net10.0 ターゲット検証済み(MEMO.md D-016) |
| ECS | [Arch](https://github.com/genaray/Arch) | |
| メッセージング | [VitalRouter](https://github.com/hadashiA/VitalRouter) | |
| リアクティブ | [R3](https://github.com/Cysharp/R3) | システム全体で使用 |
| ログ | [ZLogger](https://github.com/Cysharp/ZLogger) | AI がログを参照する機能も提供 |
| ID 等 ValueObject | [UnitGenerator](https://github.com/Cysharp/UnitGenerator) | |
| State シリアライズ | [ToonEncoder](https://github.com/Cysharp/ToonEncoder) | TOON 形式 |
| CLI | [ConsoleAppFramework](https://github.com/Cysharp/ConsoleAppFramework) | |
| AI 連携 | MCP | ゲーム依存実装は CLI に分離(MEMO.md D-001) |
| テスト | xUnit + Shouldly | |

## ソリューション構成(案)

```
Statee.slnx
├─ src/
│  ├─ Statee.Core         … State/Command/Log の抽象定義(Godot 非依存)
│  ├─ Statee.Remote       … 接続待ち受け・プロトコル実装
│  ├─ Statee.Cli          … 汎用 CLI クライアント(ConsoleAppFramework)
│  ├─ Statee.Mcp          … MCP サーバー(汎用・CLI を起動するだけ)
│  └─ Statee.Generator    … Attribute → IStateProvider 実装のソースジェネレータ(D-022)
├─ sandbox/
│  └─ PingTarget.Godot    … フレームワーク検証用の最小ダミーターゲット(D-013)
├─ game/
│  ├─ SuikaGame.Logic     … スイカゲームの純C#ロジック(Arch / R3 / VitalRouter)
│  ├─ SuikaGame.Godot     … Godot 4.7 プロジェクト(EntryPoint・描画・入力)
│  └─ SuikaGame.Cli       … ゲーム用 CLI(汎用 CLI を包む。スイカゲーム着手時に作成)
└─ tests/
   ├─ Statee.Core.Tests
   ├─ Statee.Remote.Tests
   ├─ Statee.Generator.Tests
   └─ SuikaGame.Logic.Tests
```

フレームワーク(`src/`)とサンプルゲーム(`game/`)を最初から分離し、
後の NuGet 化・別ゲームへの流用に備える。

## サンプルゲーム:スイカゲーム

- フルーツを容器に落とし、同種が衝突すると一段大きいフルーツに合体
- スコア、ゲームオーバー判定(容器から溢れる)を持つ
- シーン遷移(タイトル → ゲーム → リザルト)、UI、多数のオブジェクト状態が揃い、
  フレームワークの検証対象として十分な要素を持つ
- 物理(落下・衝突)は Godot の物理エンジンを使う(MEMO.md D-011)

**注意**: フレームワークを作る間はスイカゲームのことは忘れて設計する(MEMO.md D-013)。
フレームワークの検証には最小のダミーターゲットを使い、スイカゲームはフェーズ 3 以降で扱う。

## 開発フェーズ

| フェーズ | 内容 | 完了条件 |
|---|---|---|
| 0 | 環境検証 | Godot 4.7 × .NET 10 × 各ライブラリがビルド・動作する |
| 1 | Statee.Core | State/Command/Log の抽象定義+ユニットテスト |
| 2 | Remote / CLI / MCP 疎通 | 最小ゲーム(エコー相当)に対し MCP→CLI→ゲームで State 取得・Command 実行が通る |
| 3 | スイカゲームロジック | 純C#で合体・スコア・ゲームオーバーが動き、ユニットテストが通る |
| 4 | Godot 統合 | 描画・入力込みでプレイ可能。headless でも動作 |
| 5 | AI 自動動作確認の実証 | AI Agent が MCP 経由でゲームを操作し、動作確認シナリオを完遂する |

## 現在のマイルストーン: フェーズ 4 Godot 統合

- フェーズ 0〜2 ✅ / フェーズ 3(スイカゲームロジック、D-024)✅
- フェーズ 4 の進行: ① メインスレッドディスパッチ(D-025)✅ →
  ② SuikaGame.Godot 最小シーン(容器 + RigidBody2D 投下 + ReportContact/Merges 配線)✅ →
  ③ Statee 組み込み(drop コマンド、スコア・盤面 State 公開)→ ④ pause / step(D-003)
- 境界設計(物理・入力)の悩みどころは docs/NOTES.md に書き捨てで記録中
- フレームワーク側の先送り課題: 条件待機(GUIDELINE.md §7)。フェーズ 5 までに実装する

## 未決事項

- なし。**フェーズ 0(環境検証)は完了**:
  Godot 4.7 × net10.0(MEMO.md D-016)、Arch 実行検証・ライブラリ互換(MEMO.md D-017)
