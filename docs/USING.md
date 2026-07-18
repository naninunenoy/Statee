# USING.md — このリポジトリで自作ゲームを作る人(と AI)へ

このリポジトリをクローンして **Statee を使ったゲームを作りたい人**向けの入口。
フレームワーク(`src/`)自体を開発する人は docs/HANDOVER.md から読むこと。

> **サンプルについて**: `samples/SuikaGame.*` と `samples/RogueGame.*` は「動くデモ」であり
> **規範ではない**。今後変更・削除されうるので、写経元にしない。
> 作り方の正典はこの文書と `/new-game` skill である。

## 1. Statee で何ができるか(5分版)

Statee はゲームに組み込む**動作確認用の窓口**。ゲームが TCP(localhost、既定ポート 9310)で
待ち受け、外部(AI エージェントや CLI)から次ができる:

- **State の取得**: ゲーム内部の状態(盤面・プレイヤー・スコア等)をパスで照会する
- **コマンドの送信**: ゲームが登録した任意コマンド(移動・アイテム使用等)を実行する
- **実入力の注入**: キーイベントを本物の入力経路に流す(入力配線ごと検証できる)
- **条件待機・時間制御・ログ取得**: `wait` / freeze / step / `logs`

これにより「AI がゲームを起動し、State を見ながら操作し、期待値と照合する」
自動プレイテストが成立する。ゲームを決定論的に設計すれば(シード注入)、
**プレイ記録の再生がそのまま回帰テストになる**(リプレイ検証)。

## 2. 境界の掟(最重要)

ゲームは必ず2層に分ける:

| 層 | 置くもの | 置かないもの |
|---|---|---|
| `samples/<Name>.Logic`(純C#) | ルール・状態遷移・生成・判定のすべて | Godot API への参照 |
| `samples/<Name>.Godot`(Godot層) | 描画・入力→アクション変換・Statee 配線 | ゲームルール |

- ロジックは `tests/<Name>.Logic.Tests` で xUnit + Shouldly により厚くテストする
- Godot 層は「in: ロジックのメソッド呼び出し、out: プロパティ読み出し」だけの薄い皮に保つ
- 乱数を使うならシードを外(`--seed=` 起動引数)から注入できるようにする。
  決定論にできるゲームは決定論にする(検証能力が段違いになる)

## 3. 新しいゲームの始め方

```
/new-game <Name>
```

skill が3プロジェクト(Logic / Logic.Tests / Godot)と Statee 配線済みの
エントリポイント、専用ソリューション `samples/<Name>.slnx` を生成する
(フレームワークの `Statee.slnx` とは分離されている。D-046)。
生成直後にビルド・テスト・headless 起動が通る状態になっている。以降は docs/GUIDELINE.md の4段階
(スケルトン → 失敗するテスト → 実装 → リファクタ)でゲームを育てる。

## 4. Statee 配線チェックリスト(手で書く場合・生成物を改造する場合)

配線の定型は **`libs/Statee.Godot`** にある(D-047)。自前で複製しない:

- `StandardCommands.Register(host, node, logger)` — ping / key / screenshot / quit と
  **`system/identity` State**(Pid / StartedAt / Mvid 等。D-075)を一括登録。
  **ping は組み込みではない**ので、これを外すと疎通確認の起点を失う。
  検証シナリオの冒頭で identity を読めば「古いバイナリ・別プロセスに繋いでいた」事故を検出できる
- `KeyBinding` + `KeyBindingTable` — キーバインド表を1箇所に持ち、
  `TryHandle`(_UnhandledInput の処理)と `CreateInputStateProvider`(game/input State)の
  両方を同じ表から導出する。実装と公開情報が乖離しない
- `CmdlineArgs.ParseInt("--seed=", ...)` — シード・ポートの起動引数。
  `CmdlineArgs.HasFlag("--frozen")` で「起動直後から freeze」に対応する(D-073。
  実時間で tick が進むと接続タイミングで盤面が変わるため、再現シナリオは tick 0 から書く)
- `StateeLogging.CreateLoggerFactory(buffer)` — logs コマンド用バッファ+コンソールのロガー
- リアルタイムゲームの `tick` コマンド(入力を指定して N tick 進める)は
  `host.RegisterTickCommand(time, parseInput, step, result)`(Statee.Core、D-072)。
  ゲーム側に書くのは「引数 → 入力型の写像」と「1 tick 進める処理」だけ

ゲーム側に残る注意点:

- State クラスは `[StateeState("game/<path>")]` + `[StateeField]`。
  **CaptureState はソケットスレッドで走る**ので、メインスレッドが差し替える
  不変スナップショット(`volatile` フィールド)を読むだけにする
  (デリゲートで足りる場合は `SnapshotStateProvider`(Statee.Core)を使う)
- ゲーム状態を変えるコマンドは `RegisterMainThreadCommand`(メインスレッドで実行される)。
  読むだけ・スレッド安全なものは `RegisterCommand` でよい
- **効果音・エフェクトの発火を状態の差分から推測させない**。ロジックが
  「その tick に起きた出来事」のリスト(毎 tick 先頭でクリア)を公開し、
  Godot 層がそれを音・演出に翻訳する。「何が起きたか」はロジックの責務、
  「どう見せる・鳴らすか」は Godot 層の責務(実装例 → games/MessBreak の BattleEvent)
- `_Process` で `MainThreadDispatcher.Pump()` を毎フレーム呼ぶ。
  freeze 中も動くよう `ProcessMode = ProcessModeEnum.Always` にする
- csproj は `Godot.NET.Sdk`。**`CopyLocalLockFileAssemblies` を有効にする**
  (Godot は `.godot/mono/temp/bin` から実行するため、NuGet 依存 DLL のコピーが必要)。
  `Statee.Generator` は `OutputItemType="Analyzer"` で参照する
- headless では project.godot のウィンドウサイズが反映されない。必要なら実行時に
  `GetWindow().Size` で設定する
- `screenshot` コマンドの保存先は絶対パスで渡す(headless では撮影不可)
- **ポートを確保できなかったら即終了する(exit 1)**。バインド失敗のまま動き続けると
  CLI が別プロセス(古いバイナリ等)に繋がる事故に気づけない(D-075。雛形の
  Main.StateeServer.cs が SocketException を捕まえて `Quit(1)` する)
- **UI の見た目も検証したければ「UI ミラー State」を作る**(例 → games/MessBreak の
  `ui/hud`)。ロジック値の再掲ではなく、**画面に出している Label の文字列や
  `GetGlobalRect()` をノードから写して公開**すると、「ロジック→UI の写し間違い」
  「配置の破綻」を State で検証できる。境界の掟とは矛盾しない(State が UI を
  参照するのではなく、Godot 層が自分の表示を報告する)。注意: Godot の Control
  レイアウトはフレーム末に確定するため、**起動直後(tick 0)の Rect は仮値**。
  1 tick 進めてから読む。ゲーム内ポーズは `tick` コマンドを止めない(素通りが仕様。D-076)
- **本番ビルド(ExportRelease)には TCP 待ち受けを含めない**(D-065)。
  `StateeTcpServer` の起動・停止は `Main.StateeServer.cs`(partial + partial method)に隔離し、
  csproj で `ExportRelease` のとき `Compile Remove` + `Statee.Remote` 参照を条件付きにする。
  本番出力に `Statee.Remote.dll` が無いことが確認手段

## 5. 動作確認の回し方

```
/verify --path samples/<Name>.Godot 確認したい観点…
```

要点(詳細は skill 本文):

- Godot は .NET 版を使う。パスは環境変数 `GODOT_BIN`(D-066)
- ターゲットはバックグラウンド起動。初回のみ `--import`(完了後にクラッシュするが無視)
- 操作は MCP ツール `statee_cli`。複数引数はカンマ区切りで1つの `--arg` に入れる
- **固定秒数・固定フレームの待機は書かない**。ping リトライか `wait` コマンドで条件を待つ
- 終了は必ず `quit`(exit 0 の確認まで含む)

### エージェントプレイとリプレイ検証

State に全情報を公開しておけば、エージェントは画面なしで
「State 取得 → 行動計画 → コマンド送信 → State 照合」のループでゲームをプレイできる。
さらにロジック層で受け付けた全アクションを記録し(ActionLog パターン)、
それを State として公開すれば「記録の取得 → 同一シードで再起動 → 再生 → State 完全一致」
のリプレイ検証が成立する。画面の見た目(FoW 等の演出)と検証用 State は別物 —
**演出で情報を隠しても、State は全公開**してよい(検証のための窓口だから)。

リアルタイムゲーム(固定タイムステップ)では、ActionLog の代わりに
「Tick ごとの入力状態」を記録する。エージェントのプレイ経路は
freeze + `tick` コマンド(入力を指定して N Tick 進める)で、入力ログを RLE で
State 公開すればフレーム精度リプレイまで成立する(規約と実証 → D-049、実装例 → ShootingGame)。

## 6. 読むべき文書・読まなくていい文書

| 文書 | ゲームを作る人にとって |
|---|---|
| この USING.md | 入口。まずこれ |
| docs/GUIDELINE.md | **必読**。テスト設計・4段階ワークフロー・コーディング規約 |
| ルート CLAUDE.md「環境の知識」 | Godot の起動方法(`GODOT_BIN`)・ビルドの注意 |
| docs/ARCHITECTURE.md / HANDOVER.md / adr/ | フレームワーク開発者向け。設計判断の背景が必要になったときだけ |
