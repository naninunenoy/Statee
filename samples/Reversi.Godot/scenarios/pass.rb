# R-5: 合法手が尽きてパスが発生する局面を再現し、パスの State(手番が移らない)を検証する。
# 手順は「常に最後の合法手を選ぶ」戦略で黒がパスに追い込まれる局面を
# scratchpad movegen で探索して得たもの。

expect "タイトルからローカル2人対戦を開始する"
send "start", "mode=LocalTwoPlayer"

expect "黒がパスに追い込まれる18手を打つ"
moves = [
  [4, 5], [5, 5], [6, 5], [6, 6], [6, 7], [7, 7],
  [5, 4], [5, 7], [5, 6], [4, 6], [4, 7], [3, 7],
  [7, 6], [7, 5], [2, 3], [3, 6], [2, 7], [1, 7],
]
moves.each { |x, y| send "place", "x=#{x}", "y=#{y}" }

expect "黒のパスが MoveLog に記録され、白番のまま続くことを確認する"
turn = state "game/turn"
assert turn.include?("pass black"), "MoveLog に黒のパスが記録されるはず: #{turn}"
assert turn.include?("CurrentPlayer: White"), "パス後も白番が続くはず: #{turn}"
assert turn.include?("MoveCount: 18"), "パスは MoveCount に加算されないはず: #{turn}"
assert turn.include?("Phase: Playing"), "対局は継続中のはず: #{turn}"
