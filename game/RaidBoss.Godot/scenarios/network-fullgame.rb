# C-4: AI 自動動作確認(ドッグフーディング)。
# サーバ1 + クライアント3(client1=自分/client2/client3)で部屋ロビー(D-056)を検証する。
# client1 が部屋を立て、client2・client3 が同じ合言葉で参加し、3人揃ってから
# client1 が開始する。3人で毎Tick Attack を送り続けてボスを撃破するまで検証する
# (D-054のロックステップ)。BossMaxHp(100) / (PlayerAttackDamage(10) * 3人) = 4Tickで撃破
# できる既知の決定論的な棋譜(100→70→40→10→-20)。
# 送信はサーバの確定を待たずに先行して行える(D-054)ため、4Tick分をまとめて送ってから
# 最終的にVictoryへ揃うのを待つ(逐次待ちはしない。逐次待ちが必要なリバーシとの違い)。
#
# 事前に以下を起動しておくこと(このシナリオ自体は接続済みインスタンスへ繋ぐだけ):
#   dotnet run --project game/RaidBoss.Server -- --port=<server> --game-port=<game>
#   godot --headless --path game/RaidBoss.Godot -- --port=<client1> --game-port=<game>
#   godot --headless --path game/RaidBoss.Godot -- --port=<client2> --game-port=<game>
#   godot --headless --path game/RaidBoss.Godot -- --port=<client3> --game-port=<game>

expect "server・client2・client3 をターゲットとして登録する(既定ターゲット自身が client1)"
target :server, port: 9391
target :client2, port: 9393
target :client3, port: 9394

expect "client1が部屋を立て、client2・client3が同じ合言葉で参加する"
send "create", "room=raidboss-e2e"
on(:client2) { send "join", "room=raidboss-e2e" }
on(:client3) { send "join", "room=raidboss-e2e" }
on(:server) { wait "game/sync", "ConnectedClients", "eq", 3 }

expect "client1(部屋作成者)が3人揃ったことを確認して開始する"
send "start"
on(:server) { wait "game/raidboss", "Phase", "eq", "Playing" }

expect "3人が4TickぶんのAttackを先行送信する(確定を待たずに送れるのがD-054)"
4.times do
  send "step", "action=attack"
  on(:client2) { send "step", "action=attack" }
  on(:client3) { send "step", "action=attack" }
end

expect "サーバ・全クライアントともVictoryへ遷移し、状態が完全一致することを確認する"
wait "game/raidboss", "Phase", "eq", "Victory", 5000
wait_all [:server, :client2, :client3], "game/raidboss", "Phase", "eq", "Victory", 5000

state_self = state "game/raidboss"
state_server = on(:server) { state "game/raidboss" }
state_client2 = on(:client2) { state "game/raidboss" }
state_client3 = on(:client3) { state "game/raidboss" }
assert state_self.include?("TickCount: 4"), "client1 は4Tickで撃破したはず: #{state_self}"
assert state_self.include?("BossHp: -20"), "client1 のボスHPは-20のはず: #{state_self}"
assert state_server == state_self, "サーバの状態が client1 と一致しない: #{state_server}"
assert state_client2 == state_self, "client2 の状態が client1 と一致しない: #{state_client2}"
assert state_client3 == state_self, "client3 の状態が client1 と一致しない: #{state_client3}"
