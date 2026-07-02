# 設計メモ(トレードオフの考慮と決定の記録)

各決定に ID を振り、「決定 / 背景 / トレードオフ / 状態」を記録する。
未決のものは【未決】、暫定のものは【暫定】を付ける。

---

## D-001 AI 連携は MCP、ゲーム依存実装は CLI に分離

- **決定**: AI とのやり取りは MCP で行う。ただしゲームに依存した実装は MCP サーバーに置かず、
  別途 CLI クライアント(ConsoleAppFramework 製)を作り、MCP はその CLI を叩くだけの汎用な作りにする。
- **背景**: MCP サーバーはセッション中に再ビルドできない。ゲーム側の実装が変わるたびに
  MCP サーバーの再起動が必要になるのを避ける。
- **トレードオフ**:
  - (+) ゲーム側の変更が CLI の再ビルドだけで反映され、開発イテレーションが速い
  - (+) CLI 単体でも人間・スクリプトから操作可能(MCP なしでもデバッグできる)
  - (−) コマンドごとにプロセス起動のオーバーヘッドがかかる(動作確認用途では許容)

## D-002 State は pull 型スナップショット、TOON でシリアライズ

- **決定**: State 取得は pull 型スナップショットを基本とする。
  シリアライズは [ToonEncoder](https://github.com/Cysharp/ToonEncoder)(TOON 形式)。
- **背景**: State の主な消費者は LLM。TOON はトークン効率と LLM の読解精度に最適化された形式。
- **トレードオフ**:
  - (+) AI が読むコストが JSON より低い
  - (−) push(変更イベント)が無いため AI 側はポーリングになるが、
    pause / step と組み合わせた「進めて → 見る」の運用で足りる想定
  - (−) 汎用データ交換フォーマットとしては JSON より周辺ツールが少ない

## D-003 時間制御コマンドと headless 前提

- **決定**: 即時 pause と「N フレーム進めて pause」を提供する。`godot --headless` での実行を前提とする。
- **背景**: AI による動作確認の再現性(決定論)の要。リアルタイムに流れる時間を AI が追うのは不安定。
- **補足**: ゲームをコマンド駆動にすることで、ネットワーク対戦(リモートからのコマンド注入)にも
  自然に拡張できる設計を意識する。
- **補足(D-014 より)**: 動作完了の待機には固定フレーム数でなく「State が条件を満たすまで進める
  (タイムアウト付き)」を基本形とする(GUIDELINE.md §3.1, §7)。

## D-004 ECS は Arch

- **決定**: [Arch](https://github.com/genaray/Arch) を採用(まず使ってみる)。
- **背景**: 自作ミニ ECS / Friflo.ECS / DefaultEcs と比較し、
  アーキタイプ型で高速・純C#・開発が活発である点から選択。
- **トレードオフ**: 自作より学習コストはあるが、車輪の再発明を避けフレームワーク本体に注力できる。

## D-005 メッセージングは VitalRouter

- **決定**: [VitalRouter](https://github.com/hadashiA/VitalRouter) を採用。
- **用途**: ECS の外側のレイヤ間通信(リモートコマンド → ロジック、ロジック → 表示層への通知など)。
- **背景**: source generator ベースでゼロアロケーション。純C#で動作する。

## D-006 ID 等の ValueObject は UnitGenerator

- **決定**: [UnitGenerator](https://github.com/Cysharp/UnitGenerator) で ObjectId 等の型を生成する。
- **背景**: AI がフレームを跨いで同一オブジェクトを追跡するため、ID の型安全性と安定性が重要。

## D-007 テストは xUnit + Shouldly

- **決定**: 純C#層(Statee.Core / SuikaGame.Logic)は xUnit + Shouldly でユニットテストする。
- **背景**: Godot 非依存の層はエンジン起動なしで高速にテストできることが設計上の狙い。

## D-008 ログは ZLogger、AI がログを参照できる機能を持つ

- **決定**: [ZLogger](https://github.com/Cysharp/ZLogger) で共通ログシステムを作る。
  ファイル出力に加え、メモリ上のリングバッファに保持し、Command 経由で AI が取得できるようにする
  (レベル / カテゴリ / フレーム範囲でのフィルタを想定)。
- **背景**: State のスナップショットだけでは「いつ何が起きたか」の時系列が追えない。ログがそれを補完する。

## D-009 リアクティブは R3

- **決定**: [R3](https://github.com/Cysharp/R3) をシステム全体で使用する。
- **補足**: Godot 統合(R3.Godot)の FrameProvider を利用予定。
  純C#層では FakeFrameProvider によりテスト内で時間を制御できる利点もある。

## D-010 サンプルゲームはスイカゲーム

- **決定**: サンプルゲームはスイカゲームとする。
- **背景**: シーン遷移・UI・多数のオブジェクト・スコア・ゲームオーバー条件が揃い、
  State の3粒度(システム / シーン / オブジェクト)をすべて検証できる。
- **補足**: 物理は Godot に任せる(→ D-011)。フレームワーク開発中は
  スイカゲーム固有の事情を考慮しない(→ D-013)。

## D-011 物理は Godot の物理エンジンを使う

- **決定**: スイカゲームの物理(落下・衝突)は Godot の物理エンジン(RigidBody2D)を使う。
  純C#での物理自作や外部物理ライブラリの導入はしない。
- **背景**: フレームワーク(Statee)の目的は State/Command/Log の公開と AI 連携であり、
  物理の決定論はフレームワークの関心事ではない。サンプルゲーム固有の事情を
  フレームワーク設計に持ち込まない(→ D-013)。
- **トレードオフ**:
  - (+) 実装が楽で挙動の品質が保証される
  - (−) 物理は非決定的になるため、完全一致のリプレイ検証はできない。
    AI の動作確認は「厳密な座標一致」ではなく「合体が起きた・スコアが増えた・
    ゲームオーバーになった」等の State/Log ベースの検証で行う
  - ロジック層(合体ルール・スコア・ゲームオーバー判定)は純C#のままユニットテスト可能

## D-012 【暫定】CLI ⇔ ゲーム間トランスポートは TCP (localhost)

- **決定(暫定)**: ゲームは localhost の TCP で待ち受け、CLI は都度接続する短命プロセスとする。
  フレーミングは改行区切りのテキスト(コマンドは JSON、State ペイロードは TOON)を暫定とする。
- **背景**: CLI がコマンドごとに起動する構成(D-001)のため、シンプルな接続方式が向く。
- **状態**: フェーズ 2 の実装時に確定する。

## D-013 フレームワーク開発中はサンプルゲームの事情を持ち込まない

- **決定**: フレームワーク(Statee.Core / Remote / Mcp / CLI)を作る間は
  スイカゲームのことは忘れて設計する。検証には最小のダミーターゲットを使う。
- **背景**: フレームワークは本来ゲームに限らない汎用のもの。特定ゲームの都合
  (物理・演出など)に引きずられた抽象化を避ける。

## D-014 unity-coding-skills のエッセンスを開発ガイドラインとして採用

- **決定**: [nowsprinting/unity-coding-skills](https://github.com/nowsprinting/unity-coding-skills) から
  思想と規律を抽出し、本プロジェクト向けに翻案して **GUIDELINE.md** に定めた。
- **背景**: 同リポジトリの核は「Green means done — 信頼できるテストの緑がエージェントの完了シグナル」。
  AI にゲームの動作確認をさせる本プロジェクトの目的と同型であり、テスト設計・テストファーストの
  規律がそのまま流用できる。
- **翻案時の主な読み替え**(Unity → 本プロジェクト):
  - Edit Mode / Play Mode テスト → 純C# ユニットテスト(xUnit)/ headless Godot 統合テスト
  - 統合テストのハーネス(Unity Test Framework + test-helper 相当)→ **Statee 自身**
    (Command 駆動 → State/Log 検証)。フレームワークのドッグフーディングを兼ねる
  - `GameObjectFinder` / uGUI Operator → State での UI 幾何・操作可否の公開 + InputEvent 注入コマンド
  - `#if UNITY_INCLUDE_TESTS` によるテストシーム隔離 → `#if DEBUG` / `InternalsVisibleTo`、
    および Statee 待ち受け自体のビルド分離
  - NUnit `[Category]` → xUnit `[Trait("Category", ...)]`
- **トレードオフ**:
  - (+) テスト設計規律(1メソッド1パーティション・仕様ベース・同レイヤーの証人)により
    テスト数の膨張とフレーキーを構造的に抑えられる
  - (−) スケルトン→失敗テスト→実装→リファクタの4段階コミットは小さな変更には重い。
    機能実装に適用し、微修正には求めない
- **副産物**: ガイドラインから Statee への機能要件が導かれた(GUIDELINE.md §7):
  条件待機コマンド / UI 幾何の State 公開 / InputEvent 注入 / 安定 ID / 待ち受けのビルド分離。
  フェーズ1以降の設計に反映する。

## D-015 .cs 編集時に dotnet format + CSharpier を自動実行する hooks

- **決定**: Claude Code の PostToolUse フック(`.claude/settings.json`)で、
  Edit/Write ツールが `.cs` ファイルに触れるたびに
  `dotnet format Statee.slnx --include <対象ファイル>` → `dotnet csharpier format <対象ファイル>`
  を実行する。CSharpier は dotnet ローカルツール(`dotnet-tools.json`)としてインストール。
- **クロスプラットフォーム対応**: フックのシェルは macOS/Linux が `sh -c`、
  **Windows も Git Bash**(無い場合のみ PowerShell)。そのため
  `.claude/hooks/format-cs.sh`(POSIX sh)を正とし、settings.json のコマンドは
  `sh .claude/hooks/format-cs.sh` の1本で全 OS をカバーする。
  `format-cs.ps1` は Git Bash の無い Windows 環境向けの代替として残す(内容は同等)。
  sh 版は jq が無い環境でも動くよう sed フォールバックを持ち、
  Windows 形式パス(JSON エスケープの `\\`)も正規化する。両版とも実ペイロードで動作検証済み。
- **背景**: GUIDELINE.md §5「規約強制は文書でなく診断・ツールで行う」の実践。
  `.editorconfig`(ユーザー作成)のルールを dotnet format が適用し、レイアウトは CSharpier が統一する。
- **実行順の理由**: 整形は後に実行した側が勝つため、スタイル/アナライザ修正(dotnet format)を先、
  opinionated なレイアウト整形(CSharpier)を後にして最終形を CSharpier に委ねる。
- **トレードオフ**:
  - (+) エージェント・人間どちらの編集でもフォーマットが常に収束し、diff からノイズが消える
  - (−) .cs 編集のたびに数秒のオーバーヘッド(dotnet format のソリューションロード)。
    遅くなりすぎたら `--include` の粒度やフック発火条件で調整する
  - フォーマット失敗(コンパイルエラー中など)はブロックせず黙って抜ける(exit 0)。
    整形はベストエフォートとし、正しさの検証はテストと CI に任せる
- **補足**: 最初の csproj として `src/Statee.Core`(net10.0 classlib)と、それを含む
  `Statee.slnx` を作成済み。フックの動作は実ペイロードで検証済み。
