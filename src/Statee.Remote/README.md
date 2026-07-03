# Statee.Remote

Statee の通信層。`StateeTcpServer` が localhost の TCP で待ち受け、
改行区切りの 1 行 JSON リクエストを `StateeHost` に渡し、1 行 JSON で応答する
(プロトコルの詳細は docs/MEMO.md D-018)。

- 既定ポートはターゲット側の規約で 9310(ポート 0 指定で空きポートに割当)
- 1 接続で複数リクエスト可。不正な JSON には error 応答を返し、接続は維持する
- payload の TOON は JSON 文字列としてエスケープされる(改行フレーミングとの衝突回避)

テストは `tests/Statee.Remote.Tests`(実 TCP を使う統合テスト)。
