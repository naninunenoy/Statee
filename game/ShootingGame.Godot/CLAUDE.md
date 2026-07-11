# ShootingGame.Godot 開発指針

- **ゲームルールをここに書かない**。ルール・状態遷移は ShootingGame.Logic の仕事。
  ここは描画・入力・Statee 配線だけ(in: ロジックのメソッド、out: プロパティ読み出し)
- Statee 配線の定型(標準コマンド・キーバインド表・起動引数)は libs/Statee.Godot を
  使う。自前で複製しない(D-047)
- 検証用 State(game/shootinggame)には検証に必要な情報を全公開する。
  画面上の演出で隠すものも State では隠さない
- Godot.NET.Sdk は ImplicitUsings 無効。System 系 using を明示する。
  NuGet 依存には CopyLocalLockFileAssemblies が必要
- 論理は `_PhysicsProcess`(60Hz)で 1 Tick。エージェントのプレイ経路は
  freeze + `tick` コマンド(`--arg frames=N,input=right+shoot`。D-049)。
  入力ログは `game/shootinggame/inputs` State に「そのまま再生可能」な形式で公開する
- 絵文字は同梱の Noto Color Emoji サブセット(assets/fonts/README.md)。
  絵文字を追加するときはサブセットの再生成が必要
