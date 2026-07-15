# TodoApp.Godot 開発指針

- **ゲームルールをここに書かない**。ルール・状態遷移は TodoApp.Logic の仕事。
  ここは描画・入力・Statee 配線だけ(in: ロジックのメソッド、out: プロパティ読み出し)
- Statee 配線の定型(標準コマンド・キーバインド表・起動引数)は libs/Statee.Godot を
  使う。自前で複製しない(D-047)
- 検証用 State(game/todoapp)には検証に必要な情報を全公開する。
  画面上の演出で隠すものも State では隠さない
- Godot.NET.Sdk は ImplicitUsings 無効。System 系 using を明示する。
  NuGet 依存には CopyLocalLockFileAssemblies が必要
