# R-5: 定石数手を打ち、盤面が期待どおりに反転していることを検証する。
# 手順は Reversi.Logic の純C#予測(scratchpad movegen)と完全一致することを確認済み。

expect "タイトルからローカル2人対戦を開始する"
send "start", "mode=LocalTwoPlayer"

expect "4手打ち、反転結果を盤面 State で確認する"
send "place", "x=2", "y=3"
send "place", "x=2", "y=2"
send "place", "x=2", "y=1"
send "place", "x=1", "y=1"

board = state "game/board"
assert board.include?("Black: 4"), "黒石は4のはず: #{board}"
assert board.include?("White: 4"), "白石は4のはず: #{board}"

turn = state "game/turn"
assert turn.include?("CurrentPlayer: Black"), "4手後は黒番に戻るはず: #{turn}"
assert turn.include?("MoveCount: 4"), "MoveCount は4のはず: #{turn}"
