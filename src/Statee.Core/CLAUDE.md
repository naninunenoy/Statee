# Statee.Core 開発指針

- **Godot・トランスポートに依存させない**。ここに置くのはゲームエンジン非依存の抽象のみ。
  Godot 固有の事情はターゲット側(sandbox/ や game/)、通信は Statee.Remote に置く
- コマンドハンドラと `IStateProvider.CaptureState()` は**ソケットスレッドから呼ばれる前提**。
  スレッド安全性を壊す変更(登録の遅延初期化など)を入れない
- ハンドラの戻り値は `ToonEncoder.Encode` に渡る。TOON 化できる形
  (匿名型・record・`IReadOnlyList<T>`)を返す規約を崩さない
- `[StateeState]` / `[StateeField]` の仕様を変えるときは Statee.Generator と
  そのテストを同時に更新する(docs/adr/D-022.md)
- 変更はまず `tests/Statee.Core.Tests` の失敗するテストから(docs/GUIDELINE.md §6)
