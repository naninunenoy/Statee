# Godot の SynchronizationContext に足をすくわれた(実バグ)

- 症状: PingTarget で quit / mainthread コマンドが必ず5秒タイムアウト。
  一方 ping / state は正常、フレームも進行 → Pump は毎フレーム呼ばれているはずなのに謎
- 原因: Godot .NET は await の継続をメインスレッドへ戻す SynchronizationContext を
  インストールする。StateeTcpServer の await に ConfigureAwait(false) が無かったため、
  「ソケットスレッドで動くはずのリクエスト処理」が実は**メインスレッド上**で動いていた。
  そこで MainThreadDispatcher.Run がブロック → Pump を呼ぶべきスレッド自身が待つ
  自己デッドロック → タイムアウト
- 教訓: Godot に埋め込むサーバー/バックグラウンド処理の await には必ず
  ConfigureAwait(false)。「どのスレッドで動いているか」は Godot では
  OS.GetThreadCallerId() / GetMainThreadId() で実測できる(mainthread コマンド)
- ping / state が「正常に見えた」のも実はメインスレッド実行だった
  (ブロックしないから露見しなかっただけ)。スレッド前提の検証は
  ブロック系コマンドを流さないと気付けない

