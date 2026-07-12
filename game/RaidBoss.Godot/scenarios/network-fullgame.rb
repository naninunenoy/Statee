# B-8: AI 自動動作確認(ドッグフーディング)。
# サーバ1 + クライアント2(client1/client2)を実際に繋ぎ、両クライアントが毎Tick
# Attack を送り続けてボスを撃破するまで検証する(D-054 のロックステップ)。
# BossMaxHp(100) / PlayerAttackDamage(10) * 2人 = 5Tickで撃破できる既知の決定論的な棋譜。
# 送信はサーバの確定を待たずに先行して行える(D-054)ため、5Tick分をまとめて送ってから
# 最終的にVictoryへ揃うのを待つ(逐次待ちはしない。逐次待ちが必要なリバーシとの違い)。
#
# 事前に以下を起動しておくこと(このシナリオ自体は接続済みインスタンスへ繋ぐだけ):
#   dotnet run --project game/RaidBoss.Server -- --port=<server> --game-port=<game> --room=raidboss-e2e
#   godot --headless --path game/RaidBoss.Godot -- --port=<client1> --game-port=<game>
#   godot --headless --path game/RaidBoss.Godot -- --port=<client2> --game-port=<game>

expect "server・client2 をターゲットとして登録する(既定ターゲット自身が client1)"
target :server, port: 9391
target :client2, port: 9393

expect "自分(client1)と client2 を接続する"
send "connect", "room=raidboss-e2e"
on(:client2) { send "connect", "room=raidboss-e2e" }
on(:server) { wait "game/sync", "ConnectedClients", "eq", 2 }

expect "両クライアントが5TickぶんのAttackを先行送信する(確定を待たずに送れるのがD-054)"
5.times do
  send "step", "action=attack"
  on(:client2) { send "step", "action=attack" }
end

expect "サーバ・両クライアントともVictoryへ遷移し、状態が完全一致することを確認する"
wait "game/raidboss", "Phase", "eq", "Victory", 5000
wait_all [:server, :client2], "game/raidboss", "Phase", "eq", "Victory", 5000

state_self = state "game/raidboss"
state_server = on(:server) { state "game/raidboss" }
state_client2 = on(:client2) { state "game/raidboss" }
assert state_self.include?("TickCount: 5"), "client1 は5Tickで撃破したはず: #{state_self}"
assert state_self.include?("BossHp: 0"), "client1 のボスHPは0のはず: #{state_self}"
assert state_server == state_self, "サーバの状態が client1 と一致しない: #{state_server}"
assert state_client2 == state_self, "client2 の状態が client1 と一致しない: #{state_client2}"
