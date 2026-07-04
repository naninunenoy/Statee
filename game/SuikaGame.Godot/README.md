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

- マウス移動で投下位置を決め、左クリックで次のフルーツを落とす
- 赤い破線がゲームオーバーライン。接触済みフルーツが猶予時間(1秒)を超えて
  ラインより上に留まるとゲームオーバー

## Statee(外部操作・観測)

TCP(既定 port 9310、`-- --port=` で変更可。`-- --seed=` で乱数シード注入)で待ち受ける。

```powershell
dotnet run --project src/Statee.Cli -- send --command drop --arg x=300  # フルーツ投下
dotnet run --project src/Statee.Cli -- state --path game/board          # スコア・盤面
dotnet run --project src/Statee.Cli -- logs                             # ゲームログ
dotnet run --project src/Statee.Cli -- quit
```

時間制御(D-026): `send --command pause` / `resume` /
`send --command step --arg frames=30`(指定フレーム進めて再ポーズ。完了後に応答が返る)。

`game/board` は Score / IsGameOver / NextKind / FruitCount / Fruits(Id, Kind, X, Y)を返す。

## 構成

| ファイル | 役割 |
|---|---|
| `Main.cs` | エントリポイント。容器の構築、入力、ロジックとの境界配線、Statee 組み込み |
| `Fruit.cs` | フルーツの RigidBody2D。接触をイベントで上位へ報告するだけ |
| `BoardState.cs` | 盤面 State のスレッド境界ブリッジ(`[StateeState]`) |
