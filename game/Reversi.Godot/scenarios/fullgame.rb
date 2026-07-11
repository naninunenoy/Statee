# R-5: 終局まで打ち切り、勝敗判定を検証する。
# 「常に最初の合法手を選ぶ」戦略での60手完走。純C#予測(scratchpad movegen)と
# R-2/R-3/R-4 のE2E確認で完全一致済み(白の勝ち、黒19-白45)。

expect "タイトルからローカル2人対戦を開始する"
send "start", "mode=LocalTwoPlayer"

expect "終局まで60手を打つ"
moves = [
  [2, 3], [2, 2], [2, 1], [1, 1], [0, 1], [0, 0], [3, 2], [2, 0], [1, 0], [3, 1],
  [3, 0], [4, 0], [4, 1], [0, 2], [1, 2], [5, 1], [5, 0], [6, 0], [6, 1], [7, 1],
  [5, 4], [4, 2], [1, 4], [1, 3], [0, 4], [0, 3], [2, 4], [0, 5], [5, 3], [5, 2],
  [6, 2], [7, 0], [7, 2], [7, 3], [6, 3], [2, 5], [6, 4], [7, 4], [1, 5], [2, 6],
  [3, 5], [4, 5], [5, 5], [6, 5], [7, 5], [7, 6], [0, 6], [1, 6], [0, 7], [3, 6],
  [4, 6], [5, 6], [6, 6], [6, 7], [1, 7], [2, 7], [3, 7], [4, 7], [5, 7], [7, 7],
]
moves.each { |x, y| send "place", "x=#{x}", "y=#{y}" }

expect "終局し、白の勝ち(黒19-白45)であることを確認する"
board = state "game/board"
assert board.include?("Black: 19"), "黒は19石のはず: #{board}"
assert board.include?("White: 45"), "白は45石のはず: #{board}"

turn = state "game/turn"
assert turn.include?("Phase: Result"), "終局しているはず: #{turn}"
assert turn.include?("Winner: White"), "白の勝ちのはず: #{turn}"
assert turn.include?("MoveCount: 60"), "MoveCount は60のはず: #{turn}"
