# Statee.Remote 開発指針

- **ワイヤプロトコル(D-018)を変えるときは docs/adr/ の更新と
  Statee.Cli 側の対応を同時に行う**。CLI とここは同じ規約の両端
- 通信層は薄く保つ。リクエストの解釈・コマンド実行は StateeHost の仕事で、
  ここはフレーミング(1 行 JSON)と接続管理だけを担う
- 待ち受けは localhost 限定。外部公開する変更は入れない
- **await には必ず `ConfigureAwait(false)`**。Godot は継続をメインスレッドへ戻す
  SynchronizationContext を持ち、忘れるとメインスレッドコマンドが自己デッドロックする
  (D-025 の実バグ。SynchronizationContextTest が再現テスト)
- テストはポート 0(空きポート自動割当)を使い、固定ポートに依存しない。
  非同期待ちには必ずタイムアウトを付ける(`.WaitAsync`)
