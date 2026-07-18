# MessBreak のドッグフーディングで見えた Statee へのフィードバック

games/MessBreak の開発で感じた摩擦のメモ。対応するかは個別に判断
(対応したら ADR や docs に反映して本ノートから消す)。

現在、未対応の項目はない。

## 対応済み

2026-07-18(UI 開発のフィードバック)の対応先:

- ポート衝突時の静かな劣化 → D-075(system/identity State の標準化+バインド失敗の即死。
  雛形と MessBreak に反映)
- UI ミラー State のパターン化 → docs/USING.md §4 に記載(ヘルパー化は必要になったら)
- レイアウト確定前の Rect は仮値 → docs/USING.md §4 に注意書き
- ポーズと tick コマンドの関係 → D-076(素通りを仕様とする)
- CLI の非 ASCII 文字化け → **見送り**。CLI 側で `Console.OutputEncoding = UTF8` にすると
  MCP サーバー側のデコード(既定コードページ)と衝突して stderr が文字化けした。
  出力エンコーディングはワイヤ両端(CLI / MCP / 呼び出し側シェル)の合意が必要で、
  「MCP の再ビルドを要求する変更を入れない」(src/Statee.Cli/CLAUDE.md)に抵触するため、
  表示上の `?` は許容する(値は正常)

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
