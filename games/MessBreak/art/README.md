# MessBreak アートアセット

すべて `.sprite.txt`(テキスト定義)が正で、PNG は dotee の生成物。
複数フレームは 1 ファイルにまとめ、**横並びのスプライトシート**として出力される(D-078)。
フレームの並び順 = ファイル内の定義順。Godot 側は `Sprite2D.Hframes` + `Frame`(定義順の添字)で参照する。

変換(1 ファイル):

```
dotnet run --project tools/dotee/Dotee -- render --input games/MessBreak/art/<path>.sprite.txt
```

`*_x16.png` は確認用プレビュー(gitignore 済み)。等倍 PNG はコミットする。

## 規約

- 基準グリッドは **16x16**(画面は Zoom 3 固定 = 1 ドット 3px)。雑魚 16、ボス 32、顔アイコン 24、ピックアップ 12
- **キャラ・敵の向きは右向きが正**。左向きは `FlipH` で反転(左右別の絵は持たない)
- 色調: 環境はブルーグレー系、味方のテック光は**ティール(#5FE8D8)**、敵は**深紅系**、警告は橙。
  新アセットもこの3系統に寄せる
- 被弾フラッシュ・ヒットストップ・浮遊の上下揺れ・ドッジの回転は**コード側の表現**(絵に持たせない)

## 一覧

| ファイル | サイズ | フレーム | 用途・備考 |
|---|---|---|---|
| `attacker.sprite.txt` | 16 | 1 | 旧・正面向きプレイヤー(Main.cs が使用中。chars/ へ移行後に廃止予定) |
| `tiles/floor` | 16 | base_a, base_b, moss, crack, stain, panel | 床。base を敷き詰め、アクセントを低頻度で混ぜる |
| `tiles/wall` | 16 | front_a, front_b, front_crack, top, top_rim_s | 疑似 2.5D 壁(front=前面、top=上面、rim は前面との境) |
| `tiles/props` | 16 | crate, barrel, rubble | Y ソート用プロップ |
| `tiles/pillar` | 16x32 | 1 | 背の高いプロップ |
| `tiles/hazard` | 16 | shock_0..2 | 感電床ループアニメ |
| `tiles/door` | 16 | closed, open, lever_off, lever_on | レバー扉と操作レバー |
| `tiles/spawnpoint` | 16 | pulse_0, pulse_1 | 強敵出現ポイント(床に重ねる透過リング) |
| `enemies/watcher` | 16 | idle_0, idle_1, death_0..2 | 雑魚(単眼球)。idle は浮遊+視線移動 |
| `enemies/boss` | 32 | idle_0, idle_1, move_0, move_1, telegraph_0, telegraph_1 | 強敵。telegraph は両腕振り上げ=攻撃予兆 |
| `items/turret` | 16 | base, head | 2 レイヤー。head は中心回転・右向き 0 度 |
| `items/barricade` | 16 | intact, damaged | 設置物の状態差分 |
| `items/decoy` | 16 | decoy_0, decoy_1 | ホログラム明滅 |
| `items/trap` | 16 | armed, triggered | ショックトラップ |
| `items/chest` | 16 | closed, opening, open | 宝箱 |
| `items/pickup` | 12 | heal, charge | ピックアップ(浮遊はコードで) |
| `chars/breaker` | 16 | idle_0, idle_1, walk_0..3 | アタッカー(深紅ロング) |
| `chars/jammer` | 16 | idle_0, idle_1, walk_0..3 | デバッファー(紫テール) |
| `chars/mender` | 16 | idle_0, idle_1, walk_0..3 | バッファー(緑ボブ+白ワンピ) |
| `chars/weapon` | 16 | 1 | 共通ハンドガン。中心回転・右向き 0 度 |
| `chars/faces` | 24 | breaker, jammer, mender | 下部 UI バーのキャラ枠用アイコン |
