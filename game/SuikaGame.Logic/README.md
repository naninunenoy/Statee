# SuikaGame.Logic

スイカゲームの純C#ロジック(docs/MEMO.md D-024)。物理を持たない規則エンジンで、
落下・衝突は Godot 物理(D-011)が担い、このライブラリは結果の判定だけを行う。

- **入力**(Godot 層から呼ぶ): `ReportContact(a, b)` 衝突報告 /
  `SetOverflowing(id, bool)` ゲームオーバーライン超えの報告 / `Tick(delta)` 時間進行
- **出力**: `Merges`(合体通知。物理ボディの削除・生成に使う)/
  `Score`・`IsGameOver`(R3 ReadOnlyReactiveProperty)/ `Fruits`(場の一覧)
- **ルール**: 同種の接触で一段大きい種に合体(スコアは結果種の三角数)。
  スイカ同士は両方消滅(66点)。溢れ状態が猶予時間(既定1秒)続くとゲームオーバー
- 抽選はコンストラクタのシードで決定論(同じシード = 同じ系列)

テストは `tests/SuikaGame.Logic.Tests`。
