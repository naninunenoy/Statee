# MessBreak のドッグフーディングで見えた Statee へのフィードバック

games/MessBreak の開発で感じた摩擦のメモ。対応するかは個別に判断
(対応したら ADR や docs に反映して本ノートから消す)。

## 未対応(2026-07-18 UI 開発で発生)

### 1. ポート衝突時に「静かに劣化」する(実害あり・優先)

見た目確認用のウィンドウ版を残したまま headless を起動したところ、新プロセスは
バインド失敗(SocketException 10048)をログに吐いた後も**サーバー無しで動き続け**、
CLI は旧バイナリのプロセスに接続していた。「未知の State パス: ui/hud」という
無関係に見えるエラーから逆算してようやく気づいた。候補:

- バインド失敗時はターゲットを即終了させる(検証用途では「静かに劣化」より「即死」)
- 「接続先が意図したプロセスか」を確認できる標準 State(起動時刻・アセンブリの
  ビルド時刻・PID など)を Statee.Godot で提供する。古いバイナリへの接続事故を
  機械的に検出できるようになる

### 2. UI ミラー State のパターン化

MessBreak で「画面に出ている Label の文字列・HP バーの塗り率・各要素の画面上 Rect を
ノードの実表示から写して `ui/hud` State で公開する」を実装した(ロジック値の再掲ではなく
実表示から写すことで、ロジック→UI の写し間違い・配置の破綻を検証できる)。
やっていることは汎用(Text と GetGlobalRect の吸い上げ)なので、Statee.Godot の
ヘルパー化、または USING.md への「境界の掟と UI 検証の折り合い方」のパターン記載を検討。

### 3. レイアウト確定前の Rect は仮値(ドキュメント)

Godot の Control レイアウトはフレーム末に確定するため、起動直後(tick 0)に読んだ
Rect は仮値になる(MissionRect の幅が 1 だった)。UI 系 State は「1 tick 進めてから
読む」が定石。UI ミラーを一般化するなら USING.md 側の注意事項。

### 4. Godot 層ポーズと tick コマンドの関係整理

ポーズメニュー(Esc)はロジック tick を止めるが、エージェントの `tick` コマンドは
ロジックを直接進めるのでポーズを素通りする。MessBreak は Paused を State に公開する
だけにした。「人間のポーズ中にエージェントが盤面を進められる」を仕様とするか
禁止するかは、freeze との整理として一度決めたい。

### 5. CLI 出力で非 ASCII 記号が化ける(小粒)

State の文字列値に含む `▶` が Windows コンソールで `?` と表示された(値自体は正常)。
CLI の出力エンコーディング(UTF-8)指定で直せるはず。

## 対応済み

過去の項目の対応先(2026-07-18 に一括対応):

- tick コマンド定型のライブラリ化 → D-072(`RegisterTickCommand`)
- GameState スナップショットの自動生成 → D-074(見送り・再検討条件つき)
- 起動直後からの freeze → D-073(`--frozen`)
- verify スキルと雛形の State パス不一致 → .claude/skills/verify を修正
- 改名時の State パス取り残し → .claude/skills/new-game に注意書き
- イベント→表現翻訳パターンの定型化 → docs/USING.md §4 と /new-game の案内に記載
- 置き換えスライスでの旧 API の扱い → docs/GUIDELINE.md §6 に追記
- build-all の DLL ロック → tools/build-all.ps1 に -GamesOnly とヒント表示を追加
- PowerShell 5.1 の一括編集の罠 → docs/HANDOVER.md ハマりどころに追記
