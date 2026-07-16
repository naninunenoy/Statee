# 設計決定の記録(ADR)

設計判断を1決定=1ファイル(`D-0xx.md`)で記録する。
各ファイルは「決定 / 背景 / トレードオフ / 状態」を持つ。
未決のものは【未決】、暫定のものは【暫定】を付ける。

- 新しい決定は次の番号で `D-0xx.md` を作成し、この索引に1行追加する
- `notes/` は境界設計の悩みどころの書き捨てメモ。正式な決定になったら D-xxx へ昇格させる

## 索引

| ID | タイトル |
|---|---|
| [D-001](D-001.md) | AI 連携は MCP、ゲーム依存実装は CLI に分離 |
| [D-002](D-002.md) | State は pull 型スナップショット、TOON でシリアライズ |
| [D-003](D-003.md) | 時間制御コマンドと headless 前提 |
| [D-004](D-004.md) | ECS は Arch |
| [D-005](D-005.md) | メッセージングは VitalRouter |
| [D-006](D-006.md) | ID 等の ValueObject は UnitGenerator |
| [D-007](D-007.md) | テストは xUnit + Shouldly |
| [D-008](D-008.md) | ログは ZLogger、AI がログを参照できる機能を持つ |
| [D-009](D-009.md) | リアクティブは R3 |
| [D-010](D-010.md) | サンプルゲームはスイカゲーム |
| [D-011](D-011.md) | 物理は Godot の物理エンジンを使う |
| [D-012](D-012.md) | CLI ⇔ ゲーム間トランスポートは TCP (localhost) |
| [D-013](D-013.md) | フレームワーク開発中はサンプルゲームの事情を持ち込まない |
| [D-014](D-014.md) | unity-coding-skills のエッセンスを開発ガイドラインとして採用 |
| [D-015](D-015.md) | .cs 編集時に dotnet format + CSharpier を自動実行する hooks |
| [D-016](D-016.md) | Godot 4.7 × .NET 10 の互換性検証(フェーズ 0) |
| [D-017](D-017.md) | ライブラリの .NET 10 互換検証(フェーズ 0) |
| [D-018](D-018.md) | ping 縦切りスライスの設計(ワイヤプロトコル確定) |
| [D-019](D-019.md) | State を「起動時に確定(不変)」と「可変」でパス分割する |
| [D-020](D-020.md) | 挙動確認は Haiku のサブエージェントに委譲する |
| [D-021](D-021.md) | ワイヤ入出力のトレースは STATEE_TRACE 環境変数で opt-in |
| [D-022](D-022.md) | State 公開の宣言は Attribute、拘束の実体は interface |
| [D-023](D-023.md) | ゲームロジック層は外部 tick 駆動・壁時計禁止 |
| [D-024](D-024.md) | スイカゲームロジックの設計【暫定】 |
| [D-025](D-025.md) | Godot API を触るコマンドは MainThreadDispatcher で同期ディスパッチ |
| [D-026](D-026.md) | pause / step は TimeControl(エンジン非依存)+ 標準コマンドで提供(D-040 で freeze / step に改名) |
| [D-027](D-027.md) | フェーズ5: AI が MCP 経由で動作確認シナリオを完遂(初回実証) |
| [D-028](D-028.md) | 条件待機は wait コマンド(State フィールドの条件成立まで進める) |
| [D-029](D-029.md) | シナリオ内の例外はすべて StandardError で raise する |
| [D-030](D-030.md) | 決定記録を docs/adr 下に1決定1ファイルで分割 |
| [D-031](D-031.md) | 画面遷移は純C#の GameFlow、UI 導入は3スライスで進める |
| [D-032](D-032.md) | UI の作用は VitalRouter コマンド型との対応で公開する(配線からの導出のみ) |
| [D-033](D-033.md) | UI はコード(C#)で構築する。移行するなら「見た目だけ .tscn」 |
| [D-034](D-034.md) | 人間向け検証レポート — expect 語彙・screenshot コマンド・記録デコレータ |
| [D-035](D-035.md) | 宣言的 UI フレームワーク Declaree — IR 中心設計・C# DSL・Statee から独立 |
| [D-036](D-036.md) | SuikaGame の UI を Declaree に移行し、game/ui State を ui/tree で置き換える |
| [D-037](D-037.md) | ゲーム内ポーズ(ESC)とやり直し |
| [D-038](D-038.md) | UI 要素の安定 Name と name 指定クリック |
| [D-039](D-039.md) | キーバインドの State 公開(game/input) |
| [D-040](D-040.md) | 時間制御を pause / resume から freeze / unfreeze に改名 |
| [D-041](D-041.md) | 全 UI 要素へのツリー位置由来の安定 id(UiNodeId。GUID 不採用) |
| [D-042](D-042.md) | GameOver 画面と[タイトルへ] |
| [D-043](D-043.md) | エージェント向け入力 API はアクション単位(デバイス非依存) |
| [D-044](D-044.md) | 第2サンプルゲームは emoji タイルのターン制ローグライク |
| [D-045](D-045.md) | ゲーム開発者向け導線は USING.md + /new-game skill(サンプルは非規範) |
| [D-046](D-046.md) | ソリューション分割(Statee.slnx はフレームワークのみ、ゲームは各自の slnx) |
| [D-047](D-047.md) | Godot 側の Statee 配線イディオムを libs/Statee.Godot に共通化 |
| [D-048](D-048.md) | 第3サンプルゲームは固定タイムステップの横スクロールシューティング |
| [D-049](D-049.md) | 継続入力の注入とフレーム精度リプレイは規約(コアは変えない) |
| [D-050](D-050.md) | ネットワーク対戦基盤の方針(サーバ権威 + コマンドレプリケーション + 同期層別出し) |
| [D-051](D-051.md) | マルチインスタンス検証はシナリオ語彙の拡張のみで始める |
| [D-052](D-052.md) | マッチングは合言葉ゲートのみ(真のマッチメイキングはやらない) |
| [D-053](D-053.md) | 第4サンプルゲームは2人協力ボス戦(RaidBoss) |
| [D-054](D-054.md) | RaidBoss の同期方式は決定論ロックステップ(サーバは入力バンドルの確定係) |
| [D-055](D-055.md) | サーバ側の接続管理・ブロードキャストは ClientRegistry へ共通化 |
| [D-056](D-056.md) | RaidBoss を2〜4人可変+合言葉部屋ロビーへ拡張 |
| [D-057](D-057.md) | マッチング待機は1人でも開始可能。HP0は一時的な操作不能(全滅でDefeat) |
| [D-058](D-058.md) | RaidBossをシューティング演出化(弾の発射・着弾を決定論ロジックに追加) |
| [D-059](D-059.md) | RaidBossをリアルタイム化(自動Tick+レーン移動+予告付きボス攻撃) |
| [D-060](D-060.md) | Declaree にフォーム系ノードを追加(CheckBox/Slider/Stack/Overlay/ReorderList/FontSize) |
| [D-061](D-061.md) | Declaree に差分適用(Reconcile)を導入、Slider はドラッグ中もライブ通知 |
| [D-062](D-062.md) | ReorderList のドラッグ状態を宣言(IR)に昇格し、移動中・ドロップ先を可視化 |
| [D-063](D-063.md) | キーボード/ゲームパッド操作は Godot 標準フォーカスに乗る(掴みモード並び替え・フォーカストラップ) |
| [D-064](D-064.md) | なぜ任意メソッド呼び出し(PuerTS 的アプローチ)ではなく State/Command/Log 境界なのか |
| [D-065](D-065.md) | 本番ビルド(ExportRelease)から Statee の外部入口をビルドレベルで除外する |
| [D-066](D-066.md) | 環境依存パスを排除(GODOT_BIN / dotnet+dll 起動 / sh 版スクリプト)し Windows・macOS 両対応 |
| [D-067](D-067.md) | Statee の配布は NuGet パッケージを一次形態とする |
| [D-068](D-068.md) | サンプルゲームのディレクトリを game/ から samples/ にリネーム |

## notes(書き捨てメモ)

| ファイル | 内容 |
|---|---|
| [main-thread-dispatch](notes/main-thread-dispatch.md) | メインスレッドディスパッチの設計経緯(→ D-025 へ昇格) |
| [godot-synchronizationcontext](notes/godot-synchronizationcontext.md) | Godot の SynchronizationContext による自己デッドロック(実バグ) |
| [suika-physics-boundary](notes/suika-physics-boundary.md) | スイカゲーム最小シーンの物理 ↔ ロジック境界 |
