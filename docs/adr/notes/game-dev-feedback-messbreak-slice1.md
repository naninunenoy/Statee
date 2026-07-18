# MessBreak のドッグフーディングで見えた Statee へのフィードバック

games/MessBreak の開発で感じた摩擦のメモ。対応するかは個別に判断
(対応したら ADR や docs に反映して本ノートから消す)。

現在、未対応の項目はない。過去の項目の対応先(2026-07-18 に一括対応):

- tick コマンド定型のライブラリ化 → D-072(`RegisterTickCommand`)
- GameState スナップショットの自動生成 → D-074(見送り・再検討条件つき)
- 起動直後からの freeze → D-073(`--frozen`)
- verify スキルと雛形の State パス不一致 → .claude/skills/verify を修正
- 改名時の State パス取り残し → .claude/skills/new-game に注意書き
- イベント→表現翻訳パターンの定型化 → docs/USING.md §4 と /new-game の案内に記載
- 置き換えスライスでの旧 API の扱い → docs/GUIDELINE.md §6 に追記
- build-all の DLL ロック → tools/build-all.ps1 に -GamesOnly とヒント表示を追加
- PowerShell 5.1 の一括編集の罠 → docs/HANDOVER.md ハマりどころに追記
