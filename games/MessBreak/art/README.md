# MessBreak アートアセット

すべて `.sprite.txt`(テキスト定義)が正で、PNG は dotee の生成物。

シートの形式は 2 系統ある(D-080 の背景も参照):

- **格子シート(従来の `grid:` 1 枚)** … プレイヤー歩行シート
  (`attacker` / `debuffer`)が採用。48x48 = 16x16 のコマ × 3列 × 3行。
  行 = down(正面)/ up(背面)/ side(右向き)、列 = idle / stepA / stepB。
  レイアウト自体が仕様なので `frame:` 節は使わない(DESIGN.md「プレイヤースプライト」)
- **横並びシート(`frame: 名前` 節、D-080)** … タイル・敵・設置物・エフェクトなど
  「同寸法フレームの並び」。Godot 側は `Sprite2D.Hframes` + `Frame`(定義順の添字)で参照する

変換(1 ファイル):

```
dotnet run --project tools/dotee/Dotee -- render --input games/MessBreak/art/<path>.sprite.txt
```

`*_x16.png` は確認用プレビュー(gitignore 済み)。等倍 PNG はコミットする。

## 規約

- 基準グリッドは **16x16**(画面は Zoom 3 固定 = 1 ドット 3px)。雑魚 16、ボス 32、顔アイコン 24、ピックアップ 12
- **プレイヤーの向きは方向別のコマ**(4 方向スナップ。左向きは side の反転)。
  スプライト回転は寝転んで見えるため使わない(DESIGN.md)
- **敵・武器・タレット砲頭は右向きが 0 度**。左右反転や回転は描画側で行う
  (機械・無向性の球体は回転しても破綻しない)
- 色調: 環境はブルーグレー系、味方のテック光は**ティール(#5FE8D8)**、敵は**深紅系**、警告は橙。
  新アセットもこの3系統に寄せる
- 被弾フラッシュ・ヒットストップ・浮遊の上下揺れは**コード側の表現**(絵に持たせない)。
  演出タイマーは論理 tick に同期させる(D-079)

## 一覧

### ゲームが使用中

| ファイル | サイズ | 形式 | 用途 |
|---|---|---|---|
| `attacker.sprite.txt` | 48(16×3列×3行) | 格子 | アタッカー歩行シート(Main.cs がロード) |
| `debuffer.sprite.txt` | 48(16×3列×3行) | 格子 | デバッファー歩行シート(Main.cs がロード) |

### 未配線(表現系スライスで配線する素材)

| ファイル | サイズ | フレーム | 用途・備考 |
|---|---|---|---|
| `tiles/floor` | 16 | base_a, base_b, moss, crack, stain, panel | 床。base を敷き詰め、アクセントを低頻度で混ぜる |
| `tiles/wall` | 16 | front_a, front_b, front_crack, top, top_rim_s | 疑似 2.5D 壁(front=前面、top=上面、rim は前面との境)。ステージの `'#'` セルに対応 |
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
| `chars/weapon` | 16 | 1 | 共通ハンドガン。中心回転・右向き 0 度。「射撃していることを絵で示す手掛かりが無い」(DESIGN.md)を埋める候補 |
| `chars/faces` | 24 | breaker, jammer, mender | 下部 UI バーのキャラ枠用アイコン |

### 横向きプロトタイプ(要 4 方向化)

`chars/breaker` / `chars/jammer` / `chars/mender`(16x16、idle 2 + walk 4、右向きのみ)は
プレイヤーの向き仕様(4 方向コマ)が入る**前**に作った横向きシート。
ゲームに載せるには down / up 行を足して格子シート形式に組み直す必要がある。
side 行の素材、および 3 人目(メンダー)の配色・シルエットの決定版として残す。

名前の対応: breaker = attacker(アタッカー)、jammer = debuffer(デバッファー)、
mender = 3 人目(バッファー/ヒーラー。ゲーム内ファイル名は導入時に揃える)。
