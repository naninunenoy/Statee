# リバーシ ネット対戦 開発ロードマップ

D-050(基盤方針)・D-051(検証語彙)に基づき、リバーシ(docs/REVERSI_ROADMAP.md で
ローカル2人対戦は完成済み)をネット対戦化するまでの計画。フェーズ分割と各フェーズの
完了条件を定める。実装計画そのものではない。

## 前提・方針(D-050/D-051 の要約)

- サーバ権威。権威サーバは **純C#コンソールプロセス**(Godot 非依存)。Reversi.Logic を
  そのまま載せ、Statee も組み込んで権威 State を直接観測できるようにする
- 同期はコマンドレプリケーション。クライアントは着手コマンドをサーバへ送り、
  サーバが検証した確定コマンドの順序付きログを全クライアントへ配布する
- 同期層はフレームワーク外に別出し: 新トップレベル `syncee/`(Declaree と同じ流儀。
  コア=純C#・Statee非依存、`.Statee` 接着層は別プロジェクト)。`src/` とは相互無依存
- トランスポートは `ITransport` 抽象。実装はフェイク(インプロセス)と LiteNetLib の2つ。
  ワイヤは MemoryPack、TOON は観測用のまま
- ホスティング形態は自サーバー固定(Firebase/EOS は将来、プロトコルの形態非依存性で担保)
- 切断は検知のみ(切断 = 対局終了)。再接続は第2スライス
- 検証語彙(Statee.Scenario)を D-051 の範囲(複数ターゲット接続・宛先指定・
  クロスインスタンス wait)で拡張する。起動オーケストレータは作らない
  (`tools/` のスクリプトで賄う)

## 原則(リバーシ・フレームワークと同じ)

- **Green means done** / 4段階(スケルトン → 失敗するテスト → 実装 → リファクタ)で各段階コミット
- `syncee/` のコアは純C#・Statee 非依存で厚くテストする(フェイクトランスポートが主戦場)
- `src/`(Statee)にリバーシ・ネット対戦の事情を持ち込まない(D-013)。
  シナリオ語彙の拡張だけは D-051 の範囲で `src/Statee.Scenario` に入れる
- 固定フレーム・固定秒待機は書かない。クロスインスタンス wait も含め State を wait する

## ソリューション構成(案)

```
syncee/
├─ Syncee                  … コア(純C#。ITransport / CommandLog / 権威検証・配布ロジック)
├─ Syncee.Fake             … フェイクトランスポート(インプロセス。テストの主戦場)
├─ Syncee.LiteNetLib       … LiteNetLib トランスポート(実ソケット)
└─ Syncee.Statee           … Statee 接着層(同期状態を State 公開)
game/
├─ Reversi.Logic           … 変更なし(着手コマンドの供給元を知らない設計は R-1 で完了済み)
├─ Reversi.Server          … 権威サーバ(純C#コンソール。Reversi.Logic + Syncee + Statee)
└─ Reversi.Godot           … クライアント化(place コマンドをローカル即時適用でなくサーバへ送信)
```

## 開発フェーズ

| フェーズ | 内容 | 完了条件 |
|---|---|---|
| N-0 | syncee/ 骨格 | `Syncee`/`Syncee.Fake`/`Syncee.Statee` の型・API をスケルトンで定義、`syncee/README.md` を書く |
| N-1 | コマンドレプリケーション(フェイク) | フェイクトランスポート上で「クライアントが送った着手をサーバが検証しログへ確定し、全クライアントに配布する」がユニットテストで緑。ドメイン(リバーシ)に依存しない汎用コアであることを確認 |
| N-2 | Reversi 権威サーバ ✅ | `game/Reversi.Server`(純C#コンソール)が Reversi.Logic + Syncee(フェイク経由でなく実プロセスの起動骨格)+ Statee を組み込み、headless で起動・state 観測ができる |
| N-3 | LiteNetLib トランスポート ✅ | 実ソケットでサーバ1 + クライアント2(headless)の最小疎通(カウンタ相当ではなくリバーシの着手同期)が通る |
| N-4 | Reversi.Godot クライアント化 ✅ | Reversi.Godot の `place` コマンドがローカル即時反映でなくサーバへの送信 + 確定ログ適用に変わる。タイトルの「ネット対戦」ボタンが有効になる |
| N-5 | 切断検知 ✅ | クライアント切断をサーバが検知し、対局終了として State/Log に反映する。フェイクトランスポートでの切断注入テストを含む |
| N-6 | マルチインスタンス検証語彙 ✅ | Statee.Scenario に `target` / `on` / クロスインスタンス wait を追加(D-051)。3プロセス(サーバ+クライアント2)を使うシナリオ・起動手順は `tools/` に置く |
| N-7 | AI 自動動作確認 ✅ | サーバ1 + クライアント2 を実際に起動し、Ruby シナリオで「双方から交互に着手 → 全インスタンスの `game/board` が一致するまで待つ → 終局・勝敗判定」を検証、`--report-dir` でレポートが出る |

### N-0: syncee/ 骨格

- D-050 のコアはドメイン非依存。`Syncee` に置く型の当たり(案、実装時に見直してよい):
  - `ITransport`: 接続・送受信の最小抽象(Connect / Send(byte[]) / OnReceived イベント / Disconnect)
  - `CommandEnvelope`(MemoryPack 対象。コマンド名 + 引数 + 発行元 + 順序番号)
  - `AuthorityLog`: サーバ側で確定コマンドを順序付きで積み、配布するロール
  - `ReplicaLog`: クライアント側で確定コマンドを受け取り適用するロール
- `Syncee.Fake`: `ITransport` のインプロセス実装(freeze/step/切断注入ができる)
- `Syncee.Statee`: 同期状態(接続クライアント数・ログ長・最終確定コマンド等)を State 公開する接着層
- **完了条件**: 上記の型が(NotImplementedException でよい)コンパイルを通り、`syncee/README.md` に
  Declaree 同様の構成表と設計意図を書く。ADR の追加は不要(D-050 で決定済み)

### N-1: コマンドレプリケーション(フェイク)

- Reversi を意識しない汎用コマンド(例: 任意の文字列コマンド + 引数)で
  「クライアント→サーバ→確定→全クライアント配布」の一往復をテストする
- 検証観点: 非合法(バリデータが拒否)コマンドは確定ログに乗らない、
  複数クライアントへの配布順序が一致する、順序番号の連番性
- バリデータ(合法性判定)はサーバ側に注入する関数として渡す(ドメインロジックは
  呼び出し側=Reversi.Server が持ち、Syncee はそれを知らない)
- **完了条件**: `Syncee` 単体のユニットテストが全て緑。Reversi への依存が無いことを確認

### N-2: Reversi 権威サーバ

- `game/Reversi.Server`(コンソールアプリ、net10.0)を新規作成
- `Reversi.Logic` の `ReversiGame` をそのまま権威状態として保持し、
  Syncee のバリデータに `TryPlace` を渡す(合法性判定 = ゲームロジックそのもの)
- Statee を組み込み、`game/board` / `game/turn`(Reversi.Godot と同じ形)を State 公開
- **完了条件**: headless 相当(GUI 無し)でプロセスを起動し、ping / state / logs / quit が通る

**完了**: `game/Reversi.Server`(コンソール、net10.0)を新規作成。`AuthorityLog` に
`start`/`place` の合法性判定(副作用なし。`place` は `Board.GetLegalMoves` の pure な判定)を
注入し、`Committed` で `ReversiGame.Start`/`TryPlace` を適用して `game/board`・`game/turn` を
更新する構成にした。`BoardState`/`TurnState` は Reversi.Godot と同一形式で複製
(Rule of Three に達するまで抽出しない。D-013 の裏返し)。
`dotnet run --project src/Statee.Cli -- ping/send(start,place)/state/quit --port <N>` で
実プロセスの疎通・着手・状態観測・終了を確認済み。`game/Reversi.slnx` に追加、
ソリューション全体ビルドは0警告0エラー。

### N-3: LiteNetLib トランスポート

- `Syncee.LiteNetLib` に `ITransport` の実ソケット実装を追加
- サーバ1 + クライアント2(共に headless の最小疎通確認用ハーネス。まだ Reversi.Godot ではない)
- **完了条件**: 実プロセス間でコマンドが一往復し、フェイクと同じテストシナリオ(N-1 相当)が
  実ソケットでも成立する(薄い E2E のみ。厚いテストはフェイク側に残す)

**完了**: `syncee/Syncee.LiteNetLib` を新規作成。`LiteNetLibServerTransport`/
`LiteNetLibClientTransport` が `ITransport`/`IServerTransport` を実装し、`NetPeer.Tag` に
ラッパー自身を持たせることで LiteNetLib のコールバック(`INetEventListener`)を
Received/Disconnected イベントへ橋渡しする。ポーリング(`PollEvents`)はメインループから
明示的に呼ぶ形にし、フェイクトランスポートと同じ「呼び出し側が時間を進める」流儀を保った。
`Syncee.Tests` にループバックでの接続・双方向送受信・切断を確認する薄い実ソケットテストを
1本追加(固定待機でなく `PollUntil` で条件成立まで能動的にポーリングする)。10件全緑。
ワイヤの MemoryPack 化・`CommandEnvelope` の実配布は N-4 で着手する。

### N-4: Reversi.Godot クライアント化

- タイトルの「ネット対戦」を有効化。接続先(ホスト/ポート)を起動引数か State で指定
- `place` コマンドの扱いを変更: ローカルはこれまで通り即時適用、ネットは
  「サーバへ送信 → 確定ログが返ってきたら適用」に分岐。ロジック層(Reversi.Logic)は
  着手コマンドの供給元を知らない設計のまま(R-1 で確定済み)なので変更不要のはず
- **完了条件**: 窓ありでサーバ1 + クライアント2 起動し、人間2人が別ウィンドウから
  1局を最後まで打てる

**完了**: `CommandEnvelope`/新規 `CommandRequest` を `[MemoryPackable]` にし、
`Syncee.SyncWire`(素の Serialize/Deserialize ラッパー)を追加。Reversi.Server は
LiteNetLibServerTransport で対局クライアントを受け付け、接続ごとに ClientId を採番、
受信した `CommandRequest` を `AuthorityLog.TrySubmit` へ、確定した `CommandEnvelope` を
全クライアントへブロードキャストする。`game/sync`(Syncee.Statee の SyncStateProvider)で
接続数・確定数・最終コマンドを観測できるようにした。

Reversi.Godot 側は「ネット対戦」ボタン/`start --arg mode=Network`/盤クリック/
`place` コマンドのすべてで、ネットモード中は `_game` を直接変更せず
`CommandRequest` をサーバへ送り、サーバからの確定 `CommandEnvelope` を `ReplicaLog` 経由で
適用する形に変更(ローカル2人対戦の経路は変更なし)。`libs/Statee.Godot` の `CmdlineArgs` に
`ParseString` を追加し `--game-host=`/`--game-port=` を読めるようにした。

headless のサーバ1 + クライアント2 を実プロセスで起動し、両クライアントから交互に着手 →
サーバ・クライアント1・クライアント2 の `game/board`・`game/turn` が完全一致することを
`state` コマンドで確認済み(`game/sync` も ConnectedClients=2 を観測)。
ソリューション全体ビルド0警告0エラー、Reversi.Logic.Tests 24件全緑。

### N-5: 切断検知

- クライアント切断をサーバが検知し、対局終了として `game/turn` 等に反映(例: Winner が
  「相手の切断により不戦勝」を表す値、または Phase に切断終了を表す状態を追加)
- フェイクトランスポートで切断注入 → 検知 → 終了反映、をユニットテストで確認
- **完了条件**: フェイクの切断テストが緑、実ソケットでも1ケース手動確認

**完了**: `Reversi.Logic` に `GameEndReason`(Complete/Disconnected)と
`ReversiGame.EndByDisconnect(disconnectedPlayer)`(相手を勝者として Result へ遷移)を追加
(TDD: 失敗するテスト→実装の2段階、27件全緑)。

`game/Reversi.Server` の権威ロジックを `ReversiAuthority` クラスに切り出し(Statee 非依存。
Program.cs から `IServerTransport` を渡すだけでテスト可能に)、接続順に座席(1人目=黒・
2人目=白)を割り当て、対局中の切断を検知したら `"disconnect"` コマンドとして
AuthorityLog 経由で確定・配布し、`Game.EndByDisconnect` を適用する。
`tests/Reversi.Server.Tests`(新規)を `Syncee.Fake` の `FakeServerTransport`/`FakeTransport` で
作成し、接続・start・place・非合法手・**Playing中の切断→不戦勝**・**Title中の切断→対局継続**
の6件を検証(全緑)。

`Reversi.Godot` 側も `ApplyEnvelope` に `"disconnect"` ケースを追加し、`TurnState`/結果画面に
`EndReason` を反映(切断による不戦勝は「(相手の切断による不戦勝)」と表示)。

実ソケット(headless サーバ1+クライアント2)でも、対局中に一方の `quit` を叩いて
切断させ、残ったクライアント・サーバ双方の `game/turn` が `Phase: Result`・
`EndReason: Disconnected`・切断した座席の相手が `Winner` になることを手動確認した。
なお座席は接続順で決まるが、どのクライアントがどの色を送信できるかの制限は
まだ無い(D-050 のスコープ外。着手の正当性は「現在の手番と一致するか」のみで判定)。
ソリューション全体ビルド0警告0エラー、テスト計33件(Reversi.Logic 27+Reversi.Server 6)全緑。

### N-6: マルチインスタンス検証語彙(D-051)

- Statee.Scenario の Ruby 語彙に追加: `target(:name, port: N)` で複数ターゲットを登録、
  `on(:name) { ... }` で宛先を切り替え、複数ターゲットの State が同時に条件を満たすまで
  待つクロスインスタンス wait(語彙名は実装時に決める)
- 起動オーケストレータは作らない。サーバ+クライアント2の起動手順は `tools/` に
  PowerShell スクリプトとして置く(何をどの順で起動するかのメモ以上のものは作らない)
- **完了条件**: 上記語彙のユニットテストが緑(ScenarioRunnerTest 相当)

**完了**: `src/Statee.Scenario` の `ScenarioRunner` に `target(name, port:)`(名前付き接続)、
`on(name) { ... }`(宛先指定。ChibiRuby のブロック呼び出しで実装)、
`wait_all([name, ...], path, field, op, value, timeout_ms=nil)`(列挙した全ターゲットへ
同じ `wait` を順に送る。確定コマンドは追記のみで逆行しないため、これで
「クロスインスタンスで条件が揃うまで待つ」を満たす)を追加した。
`tests/Statee.Scenario.Tests/MultiTargetScenarioTest.cs` を新規作成し6件検証(全緑。
既存含め計33件)。

`Reversi.Godot` に `connect` コマンド(`start` と違い接続だけ行う。複数クライアントの
接続完了を揃えてから `start` するシナリオに必要)を追加。`Reversi.Server` に
`TimeControl`/`wait` を登録(D-028。リバーシは freeze/step を使わないが `wait` 自体は
フレーム進行前提のため必要)。

`tools/run-reversi-network.ps1`(サーバ1+クライアント2起動手順のメモ)と、
`game/Reversi.Godot/scenarios/network-sync.rb`(実際に `target`/`on`/`wait_all` を使い、
2クライアント接続→開始→着手→全インスタンスの盤面一致を検証するシナリオ)を追加し、
headless の実プロセス3つ(サーバ+クライアント2)で exit 0 を確認した。

### N-7: AI 自動動作確認

- `tools/` のスクリプトでサーバ1 + クライアント2(Reversi.Server + Reversi.Godot ×2)を起動し、
  Ruby シナリオで「クライアント1が着手 → サーバ経由でクライアント2に伝播 → 両クライアントの
  `game/board` が一致するまで wait」を繰り返し、終局まで検証する
- `--report-dir` で HTML レポートが出る(D-051 の「クロスインスタンス wait が
  同期検証の中核」を実演する)
- **完了条件**: AI Agent(statee-checker)が MCP 経由でシナリオを完遂し、レポートを提示できる

**完了**: `game/Reversi.Godot/scenarios/network-fullgame.rb` を新規作成。サーバ1(port 9380)+
クライアント2(client1=既定ターゲット・窓あり、client2=ターゲット名 :client2・headless)を
実プロセスで起動し、以下を検証した。

- `connect` → `game/sync.ConnectedClients == 2` を待ってから `client1` が `start` を送る
  (両クライアントの接続完了を揃えてから開始することで、確定コマンドの取りこぼしを防ぐ。N-6 と同じ設計)
- client1/client2 から交互に60手(R-5 と同じ決定論的棋譍)を送信。**各手の直後に
  サーバの `MoveCount` を待って直列化**する(確定順序はサーバがログとして保証するため、
  次の手を送る前に前の手の確定を待たないと非合法手として拒否されることが分かった —
  この直列化が無いテストでは実際に `MoveCount` が飛んで失敗した)
- 10手ごとに `wait_all` でサーバ・client2 の `MoveCount` 一致を確認(クロスインスタンス wait の実演)
- 終局後、サーバ・client1・client2 すべてで `Phase: Result`・`Winner: White`・
  黒19-白45の完全一致を確認
- `--report-dir --report-state game/turn` で実行し、client1(窓あり)のスクショ付き
  HTML レポートを生成(server/client2 は `on()` 経由のため記録対象外。D-034 どおり
  コンソールの server はスクショ非対応)

exit 0 を確認。ソリューション全体ビルド0警告0エラー、Reversi.Logic.Tests 27 +
Reversi.Server.Tests 6 全緑。これでリバーシのネット対戦化(N-0〜N-7)が完了した。

### N-7 の後: 合言葉によるマッチング(D-052)

N-0〜N-7 完了後、ユーザーからの追加要望で「合言葉を入力して2人揃ったら対戦開始する」
機能を追加した(D-052。真のマッチメイキングはスコープ外)。

- `game/Reversi.Server`: `--room=<合言葉>`(既定値 `reversi`)。LiteNetLib の
  `connectionKey`(N-3 で既にあった仕組み)をそのまま合言葉ゲートとして流用
- `game/Reversi.Godot`: タイトル画面に合言葉入力欄(`RoomInput`)を追加。
  「ネット対戦」ボタン・`start`/`connect` Statee コマンドの `room` 引数のどちらでも指定できる
- `declaree/Declaree`: `LineEdit` を新規追加(値を運ぶイベントは持たず、ホストが `Name` で
  Godot コントロールを直接参照して読む非リアクティブな方式。declaree/README.md 参照)
- 実プロセスで、正しい合言葉のクライアント2つはペアリングされ対局が成立し、
  誤った合言葉のクライアントは拒否される(`game/sync.ConnectedClients` が増えない)ことを確認した

## リスク・注意点

- **`syncee/` に Reversi の都合を持ち込まない**(N-0/N-1 で汎用性を保つ)。
  ドメイン固有の合法性判定はバリデータとして注入し、Syncee はそれを呼ぶだけにする
- LiteNetLib はターン制には過剰(D-050 のトレードオフ)だが、将来のリアルタイムゲームへの
  展開意思のため採用済み。ITransport が UDP 都合に歪まないよう注意する
- 状態スナップショット配布を持たないため、途中参加・再接続はこのロードマップの範囲外
- フェイクトランスポートを主戦場にし、実ソケット(LiteNetLib)のテストは薄く保つ
  (テストピラミッドの維持。N-3/N-7 のみ)
- 着工時に ARCHITECTURE.md に本ロードマップの参照を追記する
