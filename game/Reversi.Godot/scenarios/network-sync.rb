# N-6: マルチインスタンス検証語彙(target/on/wait_all)の実地確認。
# 既定ターゲット(このシナリオの --port)を server として扱い、client1/client2 を
# target で追加接続する。宛先指定(on)でクライアントごとに操作し、
# クロスインスタンス wait(wait_all)で全ターゲットの game/board が一致するまで待つ。

expect "client1・client2 をターゲットとして登録する"
target :client1, port: 9381
target :client2, port: 9382

expect "両クライアントを接続する(start はまだ送らない。接続完了を先に揃える)"
on(:client1) { send "connect" }
on(:client2) { send "connect" }

expect "サーバが両クライアントの接続を認識するまで待つ(確定手ログの取りこぼしを防ぐ)"
wait "game/sync", "ConnectedClients", "eq", 2

expect "client1 からネット対戦を開始する"
on(:client1) { send "start", "mode=Network" }

expect "サーバ・両クライアントの Phase が Playing になるまで待つ"
wait "game/turn", "Phase", "eq", "Playing"
wait_all [:client1, :client2], "game/turn", "Phase", "eq", "Playing"

expect "client1 側から (2,3) に着手する"
on(:client1) { send "place", "x=2", "y=3" }

expect "サーバ・両クライアントの黒石数が4になるまで待つ(反転の伝播を確認)"
wait "game/board", "Black", "eq", 4
wait_all [:client1, :client2], "game/board", "Black", "eq", 4

expect "サーバ・両クライアントの盤面が完全一致することを確認する"
server_board = state "game/board"
board1 = on(:client1) { state "game/board" }
board2 = on(:client2) { state "game/board" }
assert server_board == board1, "server と client1 の盤面が一致しない"
assert server_board == board2, "server と client2 の盤面が一致しない"
