# MessBreak 縦切り1のドッグフーディングで見えた Statee へのフィードバック

games/MessBreak の縦切り1(リアルタイム戦闘)を Statee で開発・E2E 確認したときに
感じた摩擦のメモ。対応するかは個別に判断(対応したら ADR にして本ノートから消す)。

## 1. リアルタイムゲームの「freeze + tick コマンド」定型が毎ゲーム複製される

ShootingGame(D-048/D-049)の tick コマンド(frames + input トークン、MaxTickFrames、
`_time.OnFrame()` 呼び出し、RefreshView)を MessBreak がほぼ丸写しした。
D-047 は「配線の定型は libs/Statee.Godot へ」と言っているが、この定型はまだライブラリ化
されていない。ゲームごとに違うのは「入力トークン → 入力型の写像」だけなので、
`RegisterTickCommand<TInput>(host, parseInput, tick)` のようなヘルパに吸える形。

## 2. GameState のスナップショット公開が手書きボイラープレート

BattleLogic の公開プロパティ 14 個を Snapshot record + [StateeField] プロパティ 14 個に
手で写経した(logic 側とほぼ 1:1)。フィールドを足すたび logic / Snapshot / StateeField の
3 箇所を触ることになり、追従漏れが起きやすい。Generator で
「ロジック型を指定したら public プロパティを自動で State 化」のような省力化の余地がある。
Vector2 を X/Y の float 2 本に手で平坦化するのも同種の摩擦(ネスト公開があれば不要)。

## 3. 起動から freeze までの実時間 tick が決定論を削る

headless 起動 → CLI で freeze するまでの間に実時間で tick が進む(E2E 時、freeze 時点で
TickCount=940)。今回は敵が Idle のままなので無害だったが、開始直後から動くゲームでは
「seed 注入しても接続タイミングで盤面が変わる」ことになる。
`--frozen`(起動時から freeze 済み)のような起動引数が欲しい。
再現シナリオを tick 0 から書けるようになる。

## 4. verify スキルの手順書と /new-game 雛形の State パスが不一致

.claude/skills/verify は `state --path system/platform` / `system/runtime` を基本手順に
挙げているが、雛形由来の MessBreak ターゲットは「未知の State パス」を返した
(結果的に game/ パスだけで確認は足りた)。雛形が system 系 State を登録していないなら
手順書から外すか、雛形に標準登録するかのどちらかに揃えたい。

## 5. (小)改名時に State パス文字列だけ取り残された

FirstGame → MessBreak の一括置換で、[StateeState("game/firstgame")] の小文字パスだけが
置換から漏れ、E2E で発覚した。State パスがプロジェクト名から自動導出される、
あるいは雛形生成時に nameof ベースで組み立てられていれば構造的に防げた。
