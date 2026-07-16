# N-7: AI 自動動作確認(ドッグフーディング)。
# サーバ1 + クライアント2(client1/client2)を実際に繋ぎ、両クライアントから交互に
# 60手(常に最初の合法手を選ぶ、既知の決定論的な棋譜)を送って終局まで検証する。
# 途中10手ごとに全インスタンスの MoveCount(=伝播完了)が一致するまで待ち、
# 最終的にサーバ・両クライアントとも終局(白の勝ち、黒19-白45)であることを確認する。
#
# --report-dir 使用時のスクショ対象は既定ターゲット(このシナリオの --port、= client1)。
# server はコンソールで画面が無いため on(:server) 経由の state/wait では
# スクショは記録されない(D-034)。

expect "server・client2 をターゲットとして登録する(既定ターゲット自身が client1)"
target :server, port: 9380
target :client2, port: 9382

expect "自分(client1)と client2 を接続してから開始する"
send "connect"
on(:client2) { send "connect" }
on(:server) { wait "game/sync", "ConnectedClients", "eq", 2 }
send "start", "mode=Network"
wait "game/turn", "Phase", "eq", "Playing"
wait_all [:server, :client2], "game/turn", "Phase", "eq", "Playing"

moves = [
  [2, 3], [2, 2], [2, 1], [1, 1], [0, 1], [0, 0], [3, 2], [2, 0], [1, 0], [3, 1],
  [3, 0], [4, 0], [4, 1], [0, 2], [1, 2], [5, 1], [5, 0], [6, 0], [6, 1], [7, 1],
  [5, 4], [4, 2], [1, 4], [1, 3], [0, 4], [0, 3], [2, 4], [0, 5], [5, 3], [5, 2],
  [6, 2], [7, 0], [7, 2], [7, 3], [6, 3], [2, 5], [6, 4], [7, 4], [1, 5], [2, 6],
  [3, 5], [4, 5], [5, 5], [6, 5], [7, 5], [7, 6], [0, 6], [1, 6], [0, 7], [3, 6],
  [4, 6], [5, 6], [6, 6], [6, 7], [1, 7], [2, 7], [3, 7], [4, 7], [5, 7], [7, 7],
]

expect "自分(client1)と client2 から交互に60手を送る"
moves.each_with_index do |move, i|
  x, y = move
  if i % 2 == 0
    send "place", "x=#{x}", "y=#{y}"
  else
    on(:client2) { send "place", "x=#{x}", "y=#{y}" }
  end
  # 次の手は直前の手がサーバで確定した後でないと正しい盤面に対して評価できないため、
  # サーバの MoveCount で直列化する(D-050: 確定順序はサーバがログとして保証する)
  on(:server) { wait "game/turn", "MoveCount", "eq", (i + 1), 3000 }
  if (i + 1) % 10 == 0
    wait_all [:server, :client2], "game/turn", "MoveCount", "eq", (i + 1), 5000
  end
end

expect "終局まで待ち、サーバ・両クライアントの結果が一致することを確認する"
wait "game/turn", "Phase", "eq", "Result", 15000
wait_all [:server, :client2], "game/turn", "Phase", "eq", "Result", 15000

turn_self = state "game/turn"
turn_server = on(:server) { state "game/turn" }
turn2 = on(:client2) { state "game/turn" }
assert turn_self.include?("Winner: White"), "client1 は白の勝ちのはず: #{turn_self}"
assert turn_server.include?("Winner: White"), "サーバは白の勝ちのはず: #{turn_server}"
assert turn2.include?("Winner: White"), "client2 は白の勝ちのはず: #{turn2}"

board_self = state "game/board"
board_server = on(:server) { state "game/board" }
board2 = on(:client2) { state "game/board" }
assert board_self.include?("Black: 19"), "client1 の黒は19石のはず: #{board_self}"
assert board_self.include?("White: 45"), "client1 の白は45石のはず: #{board_self}"
assert board_server == board_self, "サーバの盤面が client1 と一致しない"
assert board2 == board_self, "client2 の盤面が client1 と一致しない"
