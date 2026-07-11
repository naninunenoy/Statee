# Syncee

ネットワーク対戦の同期層(docs/adr/D-050.md)。サーバ権威 + コマンドレプリケーションを、
`src/`(Statee)とも `game/`(各ゲーム)とも相互無依存な形で提供する。Declaree と同じ流儀
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
  すべて呼び出し側(例: `game/Reversi.Server`)が持つ
- `src/`(Statee)から `Syncee` への依存は無い。`Syncee.Statee` が接着層として
  一方向に `Statee.Core` を参照するだけ(Declaree.Statee と同型)
- ワイヤシリアライズは MemoryPack。`CommandEnvelope`/`CommandRequest` は
  `[MemoryPackable]` を付けた素の C# 型で、`SyncWire`(Serialize/Deserialize の薄いラッパー)
  を呼び出し側(`game/Reversi.Server`・`Reversi.Godot`)が使う。`AuthorityLog`/`ReplicaLog`
  自体はシリアライズを知らない

## 設計の要点(D-050)

- `ITransport` は1本の接続を表す最小抽象(送受信 + 切断)。`IServerTransport` は
  新規接続の受け入れ口
- `AuthorityLog`(サーバ側)がクライアントからのコマンドを検証・確定し、
  `ReplicaLog`(クライアント側)がそれを受信順に適用する。これは決定論(D-011)・
  リプレイ規約(D-049)と同型の設計
- 状態スナップショット配布は持たない(途中参加・再接続は将来拡張。D-050 のトレードオフ)

リバーシでの実証内容は docs/ARCHITECTURE.md「サンプルゲーム:リバーシ」を参照。
