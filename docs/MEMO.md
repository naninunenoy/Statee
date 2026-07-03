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
- **検証済み(D-017)**: net10.0 で使えない場合は不採用の条件付きだったが、
  Arch 2.1.0 が .NET 10.0.1 上で動作することを確認したため採用確定。

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

## D-012 CLI ⇔ ゲーム間トランスポートは TCP (localhost)

- **決定**: ゲームは localhost の TCP で待ち受け、CLI は都度接続する短命プロセスとする。
  フレーミングは改行区切りのテキスト(コマンドは JSON、State ペイロードは TOON)。
- **背景**: CLI がコマンドごとに起動する構成(D-001)のため、シンプルな接続方式が向く。
- **状態**: ✅ D-018 で詳細を確定。

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

## D-016 Godot 4.7 × .NET 10 の互換性検証(フェーズ 0)— ✅ 検証成功

- **目的**: PLAN.md 未決事項「Godot 4.7 で net10.0 ターゲットが通るか」の検証。
- **ビルド検証: ✅ 成功**: `game/SuikaGame.Godot` を作成し、`Godot.NET.Sdk/4.7.0` +
  `<TargetFramework>net10.0</TargetFramework>` で `dotnet build` が成功
  (0警告0エラー、C# 14 の `field` キーワードもコンパイル通過)。
  Godot.NET.Sdk はデフォルト TFM が net8.0 でも上書き指定を許容する。
- **実行時検証: ✅ 成功**: Godot 4.7 **.NET 版**(`4.7.stable.mono.official`)の headless 実行で
  `FrameworkDescription: .NET 10.0.1` / C# 14 `field` キーワードの実行時動作を確認。正常終了(exit 0)。
- **注意点**:
  - Godot は**標準版と .NET 版が別バイナリ**。C# 実行には .NET 版
    (バージョン文字列が `mono.official`、exe の隣に `GodotSharp` フォルダあり)が必須。
    使用バイナリ: `Downloads\Godot_v4.7-stable_mono_win64\`
  - `--headless --import` はインポート完了後の終了時にクラッシュする(exit 0xC0000005)。
    インポート自体は完了しており実害なし。CI 等では import ステップの exit code を無視する必要がある
- **結論**: 全レイヤーを net10.0 に統一できる。マルチターゲット構成は不要。

## D-017 ライブラリの .NET 10 互換検証(フェーズ 0)— ✅ 完了

- **前提**: Arch は「net10 で使えないなら不採用」の条件付き採用(D-004)。
  Cysharp 製ライブラリ(R3 / ZLogger / ToonEncoder / UnitGenerator / ConsoleAppFramework)と
  VitalRouter は検証不要と判断(ユーザー決定)。ToonEncoder はそもそも .NET 10 が最小要件。
- **Arch: ✅ 実行検証成功**: Arch 2.1.0 を .NET 10.0.1 のコンソールアプリで実行し、
  World 生成 / エンティティ100体生成 / QueryDescription + ref ラムダでのクエリ更新 /
  コンポーネント Add・Remove / エンティティ Destroy・IsAlive がすべて正常動作。**採用確定**。
- **副次確認**: 以下すべて net10.0 プロジェクトへの restore が「互換性あり」で成功
  (検証時のバージョンの記録も兼ねる):
  - R3 1.3.1 / VitalRouter 2.7.1 / ZLogger 2.5.10 / UnitGenerator 2.0.0 / ToonEncoder 2.0.0
- **これでフェーズ 0(環境検証)は完了**: Godot 4.7 × .NET 10(D-016)+ ライブラリ互換(本項)。

## D-018 ping 縦切りスライスの設計(ワイヤプロトコル確定)

- **目的**: 「ゲーム起動 → MCP/CLI から操作 → State/ログを AI が取得・評価」の最小 E2E を、
  スイカゲームではなく ping 程度のダミーターゲットで貫通させる(フェーズ1最小分+フェーズ2)。
- **ワイヤプロトコル(D-012 の確定)**:
  - TCP localhost、デフォルトポート **9310**(CLI は `--port`、ターゲットは起動引数で変更可)
  - リクエスト = UTF-8 の1行 JSON: `{"id":"1","command":"ping","args":{"message":"hello"}}`
  - レスポンス = 1行 JSON: `{"id":"1","status":"ok","payload":"<TOON文字列>"}` /
    `{"id":"1","status":"error","error":"理由"}`
  - **payload は TOON を JSON 文字列として格納**する。TOON は複数行フォーマットのため、
    生で流すと改行区切りフレーミングと衝突する。JSON エスケープでこれを回避し、
    CLI が payload を展開して stdout に出す(AI が読むのは CLI 出力の生 TOON)
  - 1接続で複数リクエスト可。不正な JSON 行には error 応答を返し、接続は維持する
- **スライスの範囲**:
  - コマンドは `ping` / `state` / `logs` / `quit` のみ。**pause / step(D-003)は次スライス**
  - コマンドハンドラのメインスレッドディスパッチ(Godot API を触るコマンドに必要)も次スライス。
    ping ターゲットはスレッド安全な最小 State(フレーム番号・稼働時間)のみ公開する
- **配置**:
  - ダミーターゲットは `sandbox/PingTarget.Godot`(D-013 の「最小ダミーターゲット」。
    `game/` はスイカゲーム用に温存)
  - CLI はゲーム非依存の汎用 CLI として `src/Statee.Cli` に置く。
    D-001 の「ゲーム依存 CLI」はスイカゲーム着手時に汎用 CLI を包む形で別途作る
- **MCP ツール粒度**: コマンドごとにツール化せず、CLI 引数をそのまま渡す汎用1ツール。
  D-001(MCP は汎用・再ビルド不要)に忠実にする。
- **結果: ✅ E2E 成功**(実装はテストファーストの4段階コミットで実施。ユニット+統合テスト26件緑):
  - CLI 直・MCP(JSON-RPC)の両経路で ping / state / logs / quit が動作
  - state でフレーム進行(3秒で 75720→76248)と `.NET 10.0.1` / Godot 4.7 を確認
  - logs で起動〜ping履歴〜quit の時系列を取得。quit で headless Godot が exit 0 で正常終了
- **実装で得た知見**:
  - ToonEncoder.Encode は匿名型・`IReadOnlyList<LogEntry>` をそのまま TOON 化できる
  - Godot は `.godot/mono/temp/bin` から実行するため、NuGet 依存を持つ場合
    `<CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>` が必要
  - Godot.NET.Sdk は ImplicitUsings が無効(System 系の using を明示する)
  - `.mcp.json` は相対パスで書ける(MCP サーバーの CWD はプロジェクトルート)。
    ツール側で `Path.GetFullPath` により絶対化する
  - MCP ツールを Claude Code から使うには `.mcp.json` 登録後にセッション再起動が必要

## D-019 State を「起動時に確定(不変)」と「可変」でパス分割する

- **決定**: system 粒度の State を `system/platform`(起動時に確定する不変情報)と
  `system/runtime`(可変情報)の2パスに分割する。PingTarget で最小実装した。
  - `system/platform`: Engine / Runtime / Os / Headless / ProcessorCount / Pid / Port / StartedAt
  - `system/runtime`: Frame / UptimeSeconds
  - CLI の `state` の既定パスは `system/runtime` に変更
- **背景**: pull 型ポーリング(D-002)では、AI は不変情報をセッション開始時に1回だけ読み、
  以後は可変側だけをポーリングすればトークンを節約できる。
  また「不変 = _Ready で一度だけ構築したスナップショットを返す」構造は、
  ソケットスレッドから Godot API に触れない制約(D-018)とも噛み合う。
- **見送り(最小実装のため)**: 乱数シードの公開、IStateProvider への不変メタデータ
  (`IsImmutable`)とパス一覧ディスカバリ。必要になったスライスで追加する。
- **検証**: MCP ツール(`statee_cli`)経由で AI が直接 platform / runtime を取得し、
  フレーム進行と不変情報を確認。セッション再起動後の MCP 完全体もこれで検証済み。

## D-020 挙動確認は Haiku のサブエージェントに委譲する

- **決定**: 動作確認専用のサブエージェント `.claude/agents/statee-checker.md` を定義し、
  frontmatter の `model: haiku` で軽量モデルに固定する。
  ツールは Bash / Read / `mcp__statee__statee_cli` のみ(コード変更不可)。
- **背景**: ping / state / logs の取得・照合はツール往復が多い一方で高度な推論を要さない。
  サブエージェント化により (1) Haiku で高速・低コストに実行、
  (2) 確認作業のやり取りがサブエージェント側コンテキストに隔離され、
  メインセッションは要約だけを受け取るため二重にトークンを節約できる。
- **使い方**: メインの会話で「statee-checker で動作確認して」等と依頼する
  (description により自動委譲も効く)。

## D-021 ワイヤ入出力のトレースは STATEE_TRACE 環境変数で opt-in

- **決定**: 環境変数 `STATEE_TRACE` にファイルパスを設定すると、CLI がワイヤ上の
  リクエスト/レスポンス(1行 JSON)をタイムスタンプ付きで追記する。
  先頭の `~` はユーザープロファイルに展開。`.mcp.json` の env に
  `STATEE_TRACE=~/.statee/trace.log` を設定済みのため、MCP 経由の操作は常に記録される。
- **背景**: 動作確認の入出力の過程をテキストで見たい。リポジトリ内に logs/ を作って
  gitignore する案は不採用とし、リポジトリ外(`~/.statee/`)への opt-in トレースにした。
  MCP も CLI を経由する構成(D-001)なので、CLI 1箇所への実装で全経路が記録される。
- **形式**: `<ISO8601> → <リクエストJSON>` / `<ISO8601> ← <レスポンスJSON>` / `× <エラー>`。
  ファイルは UTF-8。トレースはベストエフォート(書き込み失敗しても本来の動作は継続)。
- **注意**: PowerShell 5.1 の `Get-Content` は既定で UTF-8 を正しく表示しない
  (`-Encoding utf8` を付けるか、エディタ/`cat` で見る)。
  `.mcp.json` の変更はセッション再起動後に有効。
