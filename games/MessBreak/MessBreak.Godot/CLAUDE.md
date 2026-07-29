# MessBreak.Godot 開発指針

- **ゲームルールをここに書かない**。ルール・状態遷移は MessBreak.Logic の仕事。
  ここは描画・入力・Statee 配線だけ(in: ロジックのメソッド、out: プロパティ読み出し)
- Statee 配線の定型(標準コマンド・キーバインド表・起動引数)は libs/Statee.Godot を
  使う。自前で複製しない(D-047)
- 検証用 State(game/messbreak)には検証に必要な情報を全公開する。
  画面上の演出で隠すものも State では隠さない
- Godot.NET.Sdk は ImplicitUsings 無効。System 系 using を明示する。
  NuGet 依存には CopyLocalLockFileAssemblies が必要
- 論理は `_PhysicsProcess`(60Hz)で 1 Tick。エージェントのプレイ経路は
  freeze + `tick` コマンド(`--arg frames=N,input=right+attack`。ShootingGame の D-049 と同型)
- **3D TPS 表示**: 論理は 2D(X,Y)のまま。World は Logic(X,Y) → (X, 0, Y)。
  モデルは `art/*.voxel.txt` を voxcee で GLB 化したものを実行時ロードする
  (Godot import 経路は使わない)。人間入力の WASD はカメラ相対、エージェントの
  tick トークン(left/right/up/down)はワールド絶対
- **画面ありで動かすときは、スクショを撮る直前に `Paused` を確認し true なら
  `key escape` で false に戻して再開する**(人がターミナルへ切り替える Esc で入る。D-076)。
  接続時に false でも撮影時には true になり得る。Esc はトグルなので状態を見ずに送らない
