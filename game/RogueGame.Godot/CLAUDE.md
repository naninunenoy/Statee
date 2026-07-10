# RogueGame.Godot 開発指針

- **規則をここに書かない**(D-044)。移動・戦闘・アイテム・増援・クリア判定は
  RogueGame.Logic の仕事。ここは描画・入力・Statee 配線だけ
  (in: Move / UseItem、out: プロパティ読み出しのみ)
- ターン制のため物理・タイマーは使わない。描画はアクション後の全再描画
  (`RefreshView` → `QueueRedraw`)で足りる
- FoW(探索済み記憶+視界内 Entity)は**描画層の演出**。検証用 State(game/rogue)は
  全情報を公開する。視界判定は Logic の `LineOfSight` を共用する
- 絵文字は同梱の Noto Color Emoji サブセット(assets/fonts/README.md)。
  絵文字を追加するときはサブセットの再生成が必要
- Godot.NET.Sdk は ImplicitUsings 無効。System 系 using を明示。
  NuGet 依存には `CopyLocalLockFileAssemblies` が必要(D-018 知見)
