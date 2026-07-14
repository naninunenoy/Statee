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

## サンプルゲーム:リバーシ(ローカル2人対戦・ネット対戦、全フェーズ完了 ✅)

フレームワークの流用性(スイカゲーム以外への組み込み)とネットワーク対戦基盤の
両方を実証する4つ目のサンプル。ターン制・完全決定論・物理/乱数なしという、
スイカゲーム・ShootingGame とは異なる性質を意図的に選んだ。

- **ローカル2人対戦**: 盤面・合法手・反転・パス・終局(`Reversi.Logic`、純C#で最厚にテスト)、
  headless 疎通 → コマンド駆動(`start`/`place`)→ UI(盤は Node2D 直描画、タイトル/結果は
  Declaree)、AI 自動動作確認シナリオ(定石・パス・終局)完遂。
  フレームワーク側は `libs/Statee.Godot` の共通化(既存。D-047)以外の変更が不要だった
- **ネットワーク対戦化**: サーバ権威(純C#コンソール `Reversi.Server`)+ コマンドレプリケーション
  + 同期層別出し `syncee/`(`src/`・各ゲームと相互無依存)+ `ITransport` 抽象
  (フェイク/LiteNetLib、ワイヤは MemoryPack)(D-050)。切断検知(対局中の切断は相手の
  不戦勝。D-050)。マルチインスタンス検証語彙 `target`/`on`/`wait_all`(D-051)。
  合言葉によるマッチングゲートと Declaree `LineEdit` の追加(D-052)

### 今後の候補(リバーシ発。未着手)

- 状態スナップショット配布(途中参加・観戦・再接続に必要。意図的に見送った。D-050)
- 座席とクライアントの紐付け強制(なりすまし対策。今は接続順で座席を割り当てるのみ)
- 本格的なマッチメイキング(ランダムマッチ・複数ルーム同時運用・EOS/Firebase 等の
  外部バックエンド。D-052 のスコープ外。構想は docs/NETWORK_PLAN.md)

## サンプルゲーム:RaidBoss(1〜4人協力ボス戦・決定論ロックステップ+部屋ロビー、全フェーズ完了 ✅)

Syncee がまだ実証していなかった「常時・高頻度・複数エンティティ」のリアルタイム同期
(決定論ロックステップ)を実証する5つ目のサンプル(D-053)。リバーシ(ターン制・低頻度・
即時確定)とは異なる同期パターンを意図的に選んだ。当初は最小規模(プレイヤー2人+ボス1体)に
絞ったが、後日ロビー機能(D-056)で2〜4人の可変人数に、さらにD-057でソロプレイ(1人)にも
対応した。

- **ローカル版**: 固定Tick駆動でリアルタイム制(D-059。Godot層が0.5秒ごとに自動でTickを
  進め、キー入力は次のTickの行動として消費する。壁時計を知るのはGodot層のみでD-023を維持)。
  Attackは即ダメージではなく弾を発射し(`ProjectileTravelTicks`後にボスへ着弾して
  ダメージを与える。D-058)、プレイヤーは`LaneCount`(7)本のレーンを左右移動できる。
  ボスは一定周期で対象レーンを予告し、`BossAttackWindupTicks`(2Tick)後にそのレーンへ
  攻撃する(移動で回避できる。D-059)。HPが0以下になったプレイヤーは
  `IncapacitationDuration`(3Tick)だけ操作不能になり(移動も弾の発射もできない)、
  明けると`ReviveHp`(最大HPの半分)で復帰する。全プレイヤーが同時にHP0以下になった
  ときだけDefeatへ遷移する(ソロプレイでは「全員」が自分1人なのでHP0=即Defeatになる。
  D-057)。ボス撃破でVictory。乱数を使わず攻撃レーン・着弾を決定論的に処理するため
  同一シード・同一入力列で結果が完全一致する(`RaidBoss.Logic`、D-011/D-049 と同型の
  決定論)。RaidBoss.Godot は中央にボスを表示し、予告レーンの赤帯、各プレイヤーから
  発射された弾の補間飛行をHP・操作不能状態とあわせて描画する(D-058/D-059。演出用の
  座標計算のみで、ロジックはRaidBoss.Logicに置いたまま)
- **ネットワーク対戦化**: 決定論ロックステップ(D-054)。Syncee コアに
  `TickBundleAuthority`/`TickReplicaLog`(1確定単位=1Tick分の複数クライアント入力バンドル)を
  新設し、既存の `AuthorityLog`/`ReplicaLog`(D-050。1コマンド=1手の即時確定)と並立させた。
  サーバ(純C# `RaidBoss.Server`)は物理・当たり判定を持たず、全クライアントのTick入力が
  揃うまで待って確定・配布するのみ。各クライアントは確認応答を待たず単調増加するTick番号で
  先行送信できる
- **部屋ロビー(D-056)**: `RaidBoss.Logic.GameLogic` は `Waiting`(参加待ち)フェーズと
  2〜4人の可変プレイヤー数に対応した(`Start(playerCount)` で確定)。サーバは
  `RoomManager` で合言葉ごとに複数の部屋(`RaidBossAuthority` + `ClientRegistry` の組)を
  1プロセスで同時に扱う。接続レベルの固定パスフレーズ(旧D-052方式)は撤廃し、
  接続後の最初の1通(`create`/`join` + `room`引数)で部屋へ振り分けるハンドシェイクに
  置き換えた。参加者が揃ったら部屋の誰か(通常は作成者)が `start` を送ると
  接続人数で `PlayerCount` が確定し `Playing` へ遷移する。RaidBoss.Godot にはコード構築
  (D-033)のロビーUI(参加/部屋を立てるボタン・合言葉入力・開始ボタン)を追加した。
  実サーバ+3クライアントのマルチインスタンスE2E(D-051の語彙)で、部屋作成→3人参加→
  開始→3人協調攻撃→Victory までの全インスタンスの状態完全一致を確認済み

### 今後の候補(RaidBoss発。未着手)

- CPU 対戦・観戦
- Godot 物理エンジンを使った当たり判定(決定論を壊すリスクがあるため、当面は
  `RaidBoss.Logic` 内の純粋計算のみ)。放物線軌道・複数弾種・ボス攻撃パターンの
  多様化は今後の候補(D-058/D-059)
- `Program.cs` の起動骨格(引数パース・`StateeHost`セットアップ・メインループ)が
  `Reversi.Server` と重複したままだが、State/コマンドの中身がゲームごとに大きく異なるため
  共通化は見送っている(接続管理・ブロードキャスト自体は `ClientRegistry` へ共通化済み。
  D-055)
- サーバ `game/raidboss` State は「直近に触れられた部屋」のみを映す簡易実装
  (`RoomManager.LastTouchedRoom`)。複数部屋を同時に検証したい場合は部屋ごとの
  State公開が必要になるが、現状は1プロセスにつき同時検証は1部屋のみを想定した割り切り
- 切断時の明示的な不戦勝判定(リバーシ(D-050)は対応済みだが、RaidBoss では
  現状「入力を受け取らなくなるだけ」)

## サンプル:TodoApp(UI 集約型・非ゲーム、全スライス完了 ✅)

Declaree(D-035/D-060)を育てることを主目的にした6つ目のサンプル。
「本来ゲームに限らない汎用フレームワーク」という目的の実証として、
意図的に非ゲーム(TODO アプリ)を選んだ。UI そのものが本体であり、
動的リスト・フィルタ・編集フォーム・完了トグル・ドラッグ並び替え・
スライダー(全体文字サイズ)・削除確認モーダルを持つ。

- ルールはすべて `TodoApp.Logic`(純C#・完全決定論・乱数なし)。
  モーダル中は Confirm/Cancel 以外の変更操作を拒否する不変条件をロジック層でも保証する
- UI は全面 Declaree。このために追加した汎用ノードが
  CheckBox / Slider / Stack / Overlay / ReorderList / UiNode.FontSize(D-060)
- headless E2E で、実入力経路(click/drag/type)による トグル・フィルタ・並び替え・
  編集・削除確認、および **Overlay がモーダル中の背面クリックを遮断すること**を確認済み

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

### 運用メモ

- headless のビューポートは 64x64 固定のため、UI を置くターゲットは
  `GetWindow().Size` を実行時に明示する(PingTarget Main.cs 参照)
- シード注入は `-- --seed=`。決定論的に操作・観測できる
- レポート実行(`--report-dir`)は窓あり Godot(headless は描画が無くスクショ不可)
