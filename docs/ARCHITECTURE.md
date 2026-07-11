# Statee アーキテクチャ

旧 `docs/PLAN.md`(開発計画)から、開発に区切りがついた時点で恒久的な内容を移したもの。
開発の経緯・各機能の設計判断は `docs/adr/` の D-xxx を参照。

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
  (構想は docs/NETWORK_PLAN.md)

## 時間制御・決定論

AI による自動確認の再現性を担保する要。

- 即時 freeze(時間凍結。ゲーム内ポーズと区別するための語。D-040)
- N フレーム進めて再 freeze(step 実行)
- 乱数シードの外部注入
- `godot --headless` での実行を前提とする(CI / AI 検証)

## テスト方針

詳細は **GUIDELINE.md**([unity-coding-skills](https://github.com/nowsprinting/unity-coding-skills)
のエッセンスを本プロジェクト向けに翻案したもの。docs/adr/D-014.md)。要点:

- 基本理念は「**Green means done**」— エージェントが自律開発できるよう、テストの緑を信頼できる完了シグナルに保つ
- テストはレイヤー化する: 純C#ユニットテスト(最厚)→ headless Godot 統合テスト(薄く)→
  ビジュアル検証 → 手動テスト(自動化できないもののみ)
- **統合テストのハーネスは Statee 自身**(Command 駆動 → State/Log 検証)。ドッグフーディングを兼ねる
- 機能実装はテストファーストの4段階(スケルトン → 失敗するテスト → 実装 → リファクタリング)で進め、
  各段階でコミットする
- ガイドラインから導かれた Statee への機能要件(条件待機コマンド、UI 幾何の State 公開、
  InputEvent 注入、安定 ID、待ち受けのビルド分離)は GUIDELINE.md §7 参照

## 技術スタック

| 領域 | 採用 | 備考 |
|---|---|---|
| 言語 / ランタイム | C# / .NET 10 | |
| ゲームエンジン | Godot 4.7.stable(.NET 版) | net10.0 ターゲット検証済み(docs/adr/D-016.md) |
| ECS | [Arch](https://github.com/genaray/Arch) | |
| メッセージング | [VitalRouter](https://github.com/hadashiA/VitalRouter) | |
| リアクティブ | [R3](https://github.com/Cysharp/R3) | システム全体で使用 |
| ログ | [ZLogger](https://github.com/Cysharp/ZLogger) | AI がログを参照する機能も提供 |
| ID 等 ValueObject | [UnitGenerator](https://github.com/Cysharp/UnitGenerator) | |
| State シリアライズ | [ToonEncoder](https://github.com/Cysharp/ToonEncoder) | TOON 形式 |
| CLI | [ConsoleAppFramework](https://github.com/Cysharp/ConsoleAppFramework) | |
| AI 連携 | MCP | ゲーム依存実装は CLI に分離(docs/adr/D-001.md) |
| テスト | xUnit + Shouldly | |

## ソリューション構成

ソリューションは分割されている(D-046): フレームワークは `Statee.slnx`、
各ゲームは専用の `game/<Name>.slnx`。全体の門番は `tools/build-all.ps1`。

```
Statee.slnx(フレームワーク)
├─ src/
│  ├─ Statee.Core         … State/Command/Log の抽象定義(Godot 非依存)
│  ├─ Statee.Remote       … 接続待ち受け・プロトコル実装
│  ├─ Statee.Cli          … 汎用 CLI クライアント(ConsoleAppFramework)
│  ├─ Statee.Mcp          … MCP サーバー(汎用・CLI を起動するだけ)
│  ├─ Statee.Scenario     … Ruby シナリオランナー(D-029, D-034)
│  └─ Statee.Generator    … Attribute → IStateProvider 実装のソースジェネレータ(D-022)
├─ libs/
│  └─ Statee.Godot        … Godot 側の Statee 配線イディオム(標準コマンド・
│                            キーバインド表・起動引数・ログ。D-047)
├─ declaree/
│  ├─ Declaree            … 宣言的 UI の IR(UiNode)+ reconciler(Godot 非依存。D-035)
│  └─ Declaree.Godot      … UiNode → Godot Control 変換(Godot 依存はここだけ)
├─ sandbox/
│  └─ PingTarget.Godot    … フレームワーク検証用の最小ダミーターゲット(D-013)
└─ tests/(フレームワークのテスト)

game/SuikaGame.slnx / game/RogueGame.slnx / game/ShootingGame.slnx
(サンプルゲーム。各ゲームが専用 slnx を持つ)
├─ game/<Name>.Logic      … 純C#ロジック(規則・状態遷移のすべて)
├─ game/<Name>.Godot      … Godot 4.7 プロジェクト(EntryPoint・描画・入力)
└─ tests/<Name>.Logic.Tests
```

依存方向は `game/ → libs/ → src/` の一方通行。フレームワーク(`src/`)と
サンプルゲーム(`game/`)を分離し、後の NuGet 化・別ゲームへの流用に備える。
ゲームを作る人の入口は docs/USING.md、雛形生成は `/new-game` skill(D-045)。

## サンプルゲーム:スイカゲーム

- フルーツを容器に落とし、同種が衝突すると一段大きいフルーツに合体
- スコア、ゲームオーバー判定(容器から溢れる)を持つ
- シーン遷移(タイトル → ゲーム → ゲームオーバー)、UI、多数のオブジェクト状態が揃い、
  フレームワークの検証対象として十分な要素を持つ
- 物理(落下・衝突)は Godot の物理エンジンを使う(docs/adr/D-011.md)
- フレームワーク開発中はスイカゲームの事情を持ち込まない原則で進めた(docs/adr/D-013.md)。
  フレームワークの検証には最小のダミーターゲット(PingTarget)を使う

## サンプルゲーム:横スクロールシューティング(ShootingGame)

- 検証特性の第3象限「**リアルタイム × 完全決定論**」を埋める(D-048)。
  固定タイムステップ(60Hz)の `Tick(InputState)` 駆動、運動・衝突は自前の数式で
  Godot 物理を使わない。乱数はシード由来の1系統のみ
- Arch(大量エンティティ)と VitalRouter(多対多イベント+interceptor によるイベントログ)を
  本来の規模で活用。3ウェーブ+ボス 🐙、アイテム ⭐、emoji 描画(D-044 と同方式)
- 検証の柱: 継続入力の注入(freeze + `tick` コマンド)、イベントログの State 公開、
  **フレーム精度リプレイ**(入力ログ RLE の State 公開 → 同一シードへ再生で完全一致。D-049)

## 開発フェーズ(全フェーズ完了 ✅)

| フェーズ | 内容 | 完了条件 |
|---|---|---|
| 0 | 環境検証 | Godot 4.7 × .NET 10 × 各ライブラリがビルド・動作する ✅(D-016, D-017) |
| 1 | Statee.Core | State/Command/Log の抽象定義+ユニットテスト ✅ |
| 2 | Remote / CLI / MCP 疎通 | 最小ゲームに対し MCP→CLI→ゲームで State 取得・Command 実行が通る ✅ |
| 3 | スイカゲームロジック | 純C#で合体・スコア・ゲームオーバーが動き、ユニットテストが通る ✅(D-024) |
| 4 | Godot 統合 | 描画・入力込みでプレイ可能。headless でも動作 ✅(D-025, D-026) |
| 5 | AI 自動動作確認の実証 | AI Agent が MCP 経由でゲームを操作し、動作確認シナリオを完遂する ✅(D-027) |

## 到達点(区切り時点のスナップショット)

フェーズ 5 以降に積み上げた主な機能。詳細は各 ADR を参照。

- 初回シナリオ完遂: AI が MCP 経由で合体スコア検証 → ゲームオーバー到達 → 凍結確認(D-027)
- 条件待機コマンド `wait`(D-028)
- Ruby シナリオランナー Statee.Scenario。語彙は send / state / wait / assert + expect(D-029, D-034)
- SuikaGame の UI 導入: `GameFlow` による画面遷移、`click` コマンドの InputEvent 注入(D-031)
- UI 作用の公開(Publishes / Explain。D-032)
- 人間向け検証レポート(スクショ + State + 期待/実際の自己完結 HTML。D-034)
- 宣言的 UI フレームワーク Declaree(UiNode IR → Godot Control、幾何の State 公開。D-035)
- SuikaGame UI の Declaree 移行(`ui/tree` に一本化。D-036)
- ゲーム内ポーズとやり直し、キー注入コマンド `key`(D-037)
- UI 要素の安定 Name と name 指定クリック(D-038)
- キーバインドの State 公開 `game/input`(D-039)
- 時間制御の改名 pause → freeze(D-040)
- 全 UI 要素の安定 id と `FindById`(D-041)
- GameOver 画面と[タイトルへ](D-042)

### 今後の候補(未着手)

- シナリオ拡充(連鎖合体、UI/幾何検証のシナリオ化)
- 静止判定(IsSleeping)の State 追加(D-027)
- ネットワーク対戦(方針決定済み: D-050 / D-051。構想全体は docs/NETWORK_PLAN.md)
- 別ゲーム(リバーシ)への流用検証(docs/REVERSI_ROADMAP.md)

### 運用メモ

- headless のビューポートは 64x64 固定のため、UI を置くターゲットは
  `GetWindow().Size` を実行時に明示する(PingTarget Main.cs 参照)
- シード注入は `-- --seed=`。決定論的に操作・観測できる
- レポート実行(`--report-dir`)は窓あり Godot(headless は描画が無くスクショ不可)
