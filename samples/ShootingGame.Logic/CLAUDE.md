# ShootingGame.Logic 開発指針

- **固定タイムステップ駆動・壁時計禁止**(D-048)。時間は `Tick(InputState)`(60Hz 相当)で
  だけ進む。`DateTime.Now` / `Task.Delay` / 自前スレッド・タイマーを持ち込まない
- **完全決定論を守る**。運動・衝突は自前の数式(Godot 物理禁止)。乱数はコンストラクタの
  シード由来の1系統のみ(ウェーブ出現・ドロップ抽選)。乱数の消費順を変える変更は
  リプレイ互換を壊すので、意図的な場合のみ行いテストを更新する
- **入力ログ(InputLog)は Tick と同時に記録**し、凍結(ゲームオーバー/クリア)後は
  記録しない。`Replay(seed, inputs)` の完全一致がこの層の最重要不変条件(D-049)
- エンティティ(弾・敵・アイテム)は Arch の Entity。Query 中の生成・破棄は不可なので
  収集してから反映する(`_toDestroy` / 収集リスト)
- ゲーム内イベントは VitalRouter で Publish し、EventLog(interceptor)が全件記録する。
  新イベントは GameEvents.cs に過去分詞で足す
- Godot に依存させない。描画・入力変換は ShootingGame.Godot の仕事
