# USING.md — このリポジトリで自作ゲームを作る人(と AI)へ

このリポジトリをクローンして **Statee を使ったゲームを作りたい人**向けの入口。
フレームワーク(`src/`)自体を開発する人は docs/HANDOVER.md から読むこと。

> **サンプルについて**: `game/SuikaGame.*` と `game/RogueGame.*` は「動くデモ」であり
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
| `game/<Name>.Logic`(純C#) | ルール・状態遷移・生成・判定のすべて | Godot API への参照 |
| `game/<Name>.Godot`(Godot層) | 描画・入力→アクション変換・Statee 配線 | ゲームルール |

- ロジックは `tests/<Name>.Logic.Tests` で xUnit + Shouldly により厚くテストする
- Godot 層は「in: ロジックのメソッド呼び出し、out: プロパティ読み出し」だけの薄い皮に保つ
- 乱数を使うならシードを外(`--seed=` 起動引数)から注入できるようにする。
  決定論にできるゲームは決定論にする(検証能力が段違いになる)

## 3. 新しいゲームの始め方

```
/new-game <Name>
```

skill が3プロジェクト(Logic / Logic.Tests / Godot)と Statee 配線済みの
エントリポイントを生成し、slnx に登録する。生成直後にビルド・テスト・headless 起動が
通る状態になっている。以降は docs/GUIDELINE.md の4段階
(スケルトン → 失敗するテスト → 実装 → リファクタ)でゲームを育てる。

## 4. Statee 配線チェックリスト(手で書く場合・生成物を改造する場合)

`/new-game` の生成物には全部入っているが、壊すと分かりにくいものを列挙する:

- **`ping` は自分で登録する**(組み込みではない)。疎通確認の起点なので必ず残す
- **`quit` を登録する**(`GetTree().Quit()`)。動作確認は「exit 0 で終了」まで含めて検証する
- State クラスは `[StateeState("game/<path>")]` + `[StateeField]`。
  **CaptureState はソケットスレッドで走る**ので、メインスレッドが差し替える
  不変スナップショット(`volatile` フィールド)を読むだけにする
- ゲーム状態を変えるコマンドは `RegisterMainThreadCommand`(メインスレッドで実行される)。
  読むだけ・スレッド安全なものは `RegisterCommand` でよい
- `_Process` で `MainThreadDispatcher.Pump()` を毎フレーム呼ぶ。
  freeze 中も動くよう `ProcessMode = ProcessModeEnum.Always` にする
- csproj は `Godot.NET.Sdk`。**`CopyLocalLockFileAssemblies` を有効にする**
  (Godot は `.godot/mono/temp/bin` から実行するため、NuGet 依存 DLL のコピーが必要)。
  `Statee.Generator` は `OutputItemType="Analyzer"` で参照する
- headless では project.godot のウィンドウサイズが反映されない。必要なら実行時に
  `GetWindow().Size` で設定する
- `screenshot` コマンドの保存先は絶対パスで渡す(headless では撮影不可)
- キー入力はバインド表(キー → 発行アクション+説明)として1箇所に持ち、
  `_UnhandledInput` の処理と `game/input` State の**両方をその表から導出**する。
  実装と公開情報が乖離しない

## 5. 動作確認の回し方

```
/verify --path game/<Name>.Godot 確認したい観点…
```

要点(詳細は skill 本文):

- Godot は .NET 版の `*_console.exe`(ルート CLAUDE.md「環境の知識」のパス)
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

## 6. 読むべき文書・読まなくていい文書

| 文書 | ゲームを作る人にとって |
|---|---|
| この USING.md | 入口。まずこれ |
| docs/GUIDELINE.md | **必読**。テスト設計・4段階ワークフロー・コーディング規約 |
| ルート CLAUDE.md「環境の知識」 | Godot のパス・ビルドの注意 |
| docs/ARCHITECTURE.md / HANDOVER.md / adr/ | フレームワーク開発者向け。設計判断の背景が必要になったときだけ |
