# TpsGame.Logic 開発指針

- **固定タイムステップ駆動・壁時計禁止**。時間は `Tick(InputState)`(60Hz 相当)で
  だけ進む。`DateTime.Now` / `Task.Delay` / 自前スレッド・タイマーを持ち込まない
- **完全決定論を守る**。運動・衝突は自前の数式(Godot 物理禁止)
- Godot に依存させない。描画・入力変換は TpsGame.Godot の仕事
- 出来事は毎 Tick の `Events` リストで公開する(状態差分から推測させない)
