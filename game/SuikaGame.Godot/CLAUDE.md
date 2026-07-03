# SuikaGame.Godot 開発指針

- **規則をここに書かない**(D-011, D-024)。合体・スコア・ゲームオーバーの判定は
  SuikaGame.Logic の仕事。ここは物理・描画・入力と、境界の配線だけ
  (in: ReportContact / SetOverflowing / Tick、out: Merges / Score / IsGameOver)
- **物理コールバック中のノード操作に注意**。`body_entered` は物理フラッシュ中に届く。
  削除は `QueueFree`、追加は `Callable.From(...).CallDeferred()` で次フレームへ
- 時間は `_PhysicsProcess` から `Tick(delta)` で流し込む。ここに独自タイマーを持たない
  (pause/step 対応の前提。D-023)
- フルーツのノード名は `Fruit_{id}` の安定 ID(GUIDELINE 3.4)。追跡可能性を壊さない
- 境界設計で悩んだら docs/NOTES.md に書き捨てで記録し、確定したら MEMO.md へ昇格
- Godot.NET.Sdk は ImplicitUsings 無効。System 系 using を明示。
  NuGet 依存(R3 等は Logic から推移)には `CopyLocalLockFileAssemblies` が必要(D-018 知見)
