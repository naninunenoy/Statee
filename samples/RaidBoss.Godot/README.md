# RaidBoss.Godot

1〜4人協力ボス戦(RaidBoss)の Godot 4.7(.NET)プロジェクト。描画・入力・ロビーUIを担い、
ゲームルールは `RaidBoss.Logic`、ネット対戦の確定・配布は `RaidBoss.Server` に委ねる
(D-053〜D-058)。

## 遊び方(ネット対戦の開始手順)

ネット対戦にはサーバプロセス(`RaidBoss.Server`)が必要。先に立ち上げる。

```powershell
# 1. サーバを起動する(Statee port 9312 / ゲーム接続 port 9412。1プロセスで複数部屋を扱える)
dotnet run --project samples/RaidBoss.Server

# 2. クライアント(人数分)を起動する(要 .NET 版 Godot)
<godot> --path samples/RaidBoss.Godot
```

クライアントの画面上の流れ:

1. 合言葉(既定 `raidboss`)を入力する
2. 1人目は「部屋を立てる」、2人目以降は同じ合言葉で「参加する」
3. 人数(1〜4人。ソロ可)が揃ったら誰かが「開始」を押す → 全員がゲーム開始
4. リアルタイム制(D-059): 0.5秒ごとにTickが自動で進む。`Q` で攻撃(弾を発射。
   2Tick後にボスへ着弾)、`A` / `D` で左右のレーンへ移動。1Tickに実行できる行動は1つ
5. ボスは赤い帯でレーンを予告し、2Tick後にそのレーンへ攻撃してくる。帯から逃げれば回避できる
6. ボスHPを0にすれば勝利。被弾3発でHP0になると一定Tick操作不能(その後半分のHPで復帰)、
   全員同時にHP0だと敗北

サーバが起動していない・合言葉が違う(参加先の部屋がない)・同名の部屋を二重に
立てた場合は、エラーダイアログが表示されてロビーからやり直せる。

ローカル動作確認だけならサーバなしでも遊べる: 接続せずにいずれかのキーを押すと
2人ローカル対戦として即開始する(プレイヤー2は `W` 攻撃、`←` / `→` 移動)。

起動引数: `-- --port=`(Statee 待受)/ `-- --game-host=` / `-- --game-port=`(サーバ接続先)/
`-- --seed=`(乱数シード)。

## Statee(外部操作・観測)

TCP(既定 port 9311)で待ち受ける。headless でも同じ手順で動く。

```powershell
dotnet run --project src/Statee.Cli -- send --command create --arg room=あいことば --port 9311  # 部屋を立てる
dotnet run --project src/Statee.Cli -- send --command join --arg room=あいことば --port 9311    # 部屋に参加
dotnet run --project src/Statee.Cli -- send --command start --port 9311                        # ゲーム開始
dotnet run --project src/Statee.Cli -- send --command freeze --port 9311                       # 自動Tick停止(決定論検証用)
dotnet run --project src/Statee.Cli -- send --command step --arg action=attack --port 9311     # 自分の入力を1Tick分送信(attack / left / right / idle)
dotnet run --project src/Statee.Cli -- state --path game/raidboss --port 9311                  # Tick・HP・レーン・弾・予告・フェーズ
dotnet run --project src/Statee.Cli -- logs --port 9311
dotnet run --project src/Statee.Cli -- quit --port 9311
```

未接続(ローカル対戦)時の `step` は `--arg player1=attack,player2=idle` の形で両者の行動を指定する。
サーバ側 State は `state --path game/raidboss --port 9312`(権威側)と `game/sync`(確定Tick数)で観測できる。
3クライアントのフルゲームE2Eは `scenarios/network-fullgame.rb` を参照。

## 構成

| ファイル | 役割 |
|---|---|
| `Main.cs` | エントリポイント。ロビーUI・描画・入力・サーバ接続・Statee 組み込み |
| `GameState.cs` | ゲーム状態 State のスレッド境界ブリッジ(`[StateeState]`) |
