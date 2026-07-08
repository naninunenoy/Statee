# SuikaGame.Godot

スイカゲームの Godot 4.7(.NET)プロジェクト。物理(落下・衝突)・描画・入力を担い、
規則(合体・スコア・ゲームオーバー)は `SuikaGame.Logic` に委ねる。

## 実行

```powershell
# エディタなし実行(要 .NET 版 Godot)
<godot> --path game/SuikaGame.Godot

# headless スモーク(チェリー2個を縦積み投下し、合体を検出したら自動終了)
<godot> --headless --path game/SuikaGame.Godot -- --smoke
```

- 起動するとタイトル画面。「はじめる」でプレイ開始、「おわる」で終了
- マウス移動で投下位置を決め、左クリックで次のフルーツを落とす(プレイ中のみ)
- ESC でポーズ/解除のトグル(D-037)。ポーズ中は「続ける」(そのまま再開)、
  「やり直す」(フルーツとスコアをリセットして再開)、「終了」(ゲームを閉じる)を選べる
- 赤い破線がゲームオーバーライン。接触済みフルーツが猶予時間(1秒)を超えて
  ラインより上に留まるとゲームオーバー

## Statee(外部操作・観測)

TCP(既定 port 9310、`-- --port=` で変更可。`-- --seed=` で乱数シード注入)で待ち受ける。

```powershell
dotnet run --project src/Statee.Cli -- send --command start             # タイトル → プレイ開始
dotnet run --project src/Statee.Cli -- send --command drop --arg x=300  # フルーツ投下(プレイ中のみ)
dotnet run --project src/Statee.Cli -- state --path game/scene          # 画面フェーズ(Title / Playing)
dotnet run --project src/Statee.Cli -- state --path ui/tree             # UI ツリー(幾何 Rect 込み。D-036)
dotnet run --project src/Statee.Cli -- send --command click --arg x=32,y=28  # 実入力経路の左クリック
dotnet run --project src/Statee.Cli -- send --command key --arg key=escape   # 実入力経路のキー入力(ESC でポーズ)
dotnet run --project src/Statee.Cli -- state --path game/board          # スコア・盤面
dotnet run --project src/Statee.Cli -- logs                             # ゲームログ
dotnet run --project src/Statee.Cli -- quit
```

時間制御(D-026): `send --command pause` / `resume` /
`send --command step --arg frames=30`(指定フレーム進めて再ポーズ。完了後に応答が返る)。

条件待機(D-028): State のフィールドが条件を満たすまで進めて待つ。
複数引数はカンマ区切りで指定する。

```powershell
dotnet run --project src/Statee.Cli -- send --command wait --arg path=game/board,field=Score,op=ge,value=1
```

`game/board` は Score / IsGameOver / NextKind / FruitCount / Fruits(Id, Kind, X, Y)を返す。
UI は Declaree(D-035, D-036)で宣言され、`ui/tree` が記述子ツリー
(Type / Props / Children / Rect)を返す。Button の `onClick` は押下時に発行される
VitalRouter コマンド型名(D-032 の Publishes 相当)、`explain` は人間向けのヒント。
UI の操作は `click`(InputEvent 注入。非表示・無効なボタンには正しく「効かない」)で行う。
ボタンの座標は決め打ちせず、`ui/tree` の Rect から中心を導出してクリックする。
ルート要素の Rect がビューポート全体を表す(headless の 64x64 問題は
`GetWindow().Size` の明示で回避済み)。

## 構成

| ファイル | 役割 |
|---|---|
| `Main.cs` | エントリポイント。容器の構築、入力、ロジックとの境界配線、Statee 組み込み |
| `Fruit.cs` | フルーツの RigidBody2D。接触をイベントで上位へ報告するだけ |
| `BoardState.cs` | 盤面 State のスレッド境界ブリッジ(`[StateeState]`) |
| `SceneState.cs` | 画面フェーズ State のスレッド境界ブリッジ(`[StateeState]`) |

UI の State(`ui/tree`)は手書きブリッジではなく Declaree.Statee の
`UiStateProvider` で公開する(D-036)。
