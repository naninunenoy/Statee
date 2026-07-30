# MessBreak.Godot 開発指針

- **ゲームルールをここに書かない**。ルール・状態遷移は MessBreak.Logic の仕事。
  ここは描画・入力・Statee 配線だけ(in: ロジックのメソッド、out: プロパティ読み出し)
- **上下関係のある責務は partial にせずレイヤーを分ける**(行数で機械的に割らない)。
  **`Main.cs` の目安はおおよそ 400 行**(オーケストレーションが肥大化したら下位レイヤーへ逃がす):
  - `Main.cs` — ライフサイクル・論理 tick・Statee 配線
  - `GameCamera.cs` — 論理↔描画座標・エイム寄りカメラ
  - `BattleSprites.cs` — スプライト／効果音アセットとアクター描画
  - `BattleView.cs` — 描画ノード・演出タイマー・盤面描画
  - `HudView.cs` — UI バー・ミッションガイド・ポーズ
  - `TickInputReader.cs` — 人間入力と tick トークン → TickInput
  - `Main.StateeServer.cs` — 同層の TCP 待ち受けだけ partial で残す(ExportRelease 除外、D-065)
  ※ Godot の `GodotObject` 派生はソースジェネレータ都合で `partial` 必須(GD0001)。
    これはレイヤー分割の partial とは別物。
- Statee 配線の定型(標準コマンド・キーバインド表・起動引数)は libs/Statee.Godot を
  使う。自前で複製しない(D-047)
- 検証用 State(game/messbreak)には検証に必要な情報を全公開する。
  画面上の演出で隠すものも State では隠さない
- Godot.NET.Sdk は ImplicitUsings 無効。System 系 using を明示する。
  NuGet 依存には CopyLocalLockFileAssemblies が必要
- 論理は `_PhysicsProcess`(60Hz)で 1 Tick。エージェントのプレイ経路は
  freeze + `tick` コマンド(`--arg frames=N,input=right+attack`。ShootingGame の D-049 と同型)
- **画面ありで動かすときは、スクショを撮る直前に `Paused` を確認し true なら
  `key escape` で false に戻して再開する**(人がターミナルへ切り替える Esc で入る。D-076)。
  接続時に false でも撮影時には true になり得る。Esc はトグルなので状態を見ずに送らない
