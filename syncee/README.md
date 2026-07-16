# Syncee

> **Experimental**: Syncee は実験的なモジュールです。API・設計は予告なく変わる可能性があります。

ネットワーク対戦の同期層(docs/adr/D-050.md)。サーバ権威 + コマンドレプリケーションを、
`src/`(Statee)とも `samples/`(各ゲーム)とも相互無依存な形で提供する。Declaree と同じ流儀
(コア + ゲーム固有 SDK 非依存 + `.Statee` 接着層)を踏襲する。

```
Syncee              … コア(純C#。ITransport / CommandEnvelope / AuthorityLog / ReplicaLog)
Syncee.Fake         … フェイクトランスポート(インプロセス。テストの主戦場)
Syncee.LiteNetLib   … LiteNetLib による実ソケットトランスポート
Syncee.Statee       … 同期状態を Statee の State として公開する接着層
```

## 境界

- **Syncee はドメイン(リバーシ等)を知らない**。合法性判定は `AuthorityLog` の
  コンストラクタに `Func<clientId, command, args, bool>` として注入する。確定コマンドの
  適用も `ReplicaLog` に `Action<CommandEnvelope>` として注入する。ゲーム固有の知識は
  すべて呼び出し側(例: `samples/Reversi.Server`)が持つ
- `src/`(Statee)から `Syncee` への依存は無い。`Syncee.Statee` が接着層として
  一方向に `Statee.Core` を参照するだけ(Declaree.Statee と同型)
- ワイヤシリアライズは MemoryPack。`CommandEnvelope`/`CommandRequest` は
  `[MemoryPackable]` を付けた素の C# 型で、`SyncWire`(Serialize/Deserialize の薄いラッパー)
  を呼び出し側(`samples/Reversi.Server`・`Reversi.Godot`)が使う。`AuthorityLog`/`ReplicaLog`
  自体はシリアライズを知らない

## 設計の要点(D-050)

- `ITransport` は1本の接続を表す最小抽象(送受信 + 切断)。`IServerTransport` は
  新規接続の受け入れ口
- `AuthorityLog`(サーバ側)がクライアントからのコマンドを検証・確定し、
  `ReplicaLog`(クライアント側)がそれを受信順に適用する。これは決定論(D-011)・
  リプレイ規約(D-049)と同型の設計
- リアルタイム・高頻度・複数エンティティの同期には `TickBundleAuthority`/`TickReplicaLog`
  (決定論ロックステップ。D-054)を使う。`AuthorityLog`/`ReplicaLog`(1コマンド=1手の
  即時確定)とは確定単位の意味が異なるため、無理に統合していない
- サーバ側の接続管理(client-N 採番・受信のディスパッチ・切断通知・ブロードキャスト)は
  確定モデルによらず共通のため `ClientRegistry` へ切り出してある(D-055)
- 複数の部屋(合言葉違い)を1プロセスで同時に扱いたい場合、受け入れ済みの接続を
  後から流し込める `ManualServerTransport` を使う。ロビー層(呼び出し側。例:
  `RaidBoss.Server.RoomManager`)が生の接続を最初の1通で振り分け、部屋ごとの
  `ManualServerTransport.Accept` へ引き渡す(D-056)
- 状態スナップショット配布は持たない(途中参加・再接続は将来拡張。D-050 のトレードオフ)

リバーシでの実証内容は docs/ARCHITECTURE.md「サンプルゲーム:リバーシ」を参照。
