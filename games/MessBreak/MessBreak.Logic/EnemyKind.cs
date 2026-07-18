namespace MessBreak.Logic;

/// <summary>敵の種別。雑魚(Mob)を倒すと敵エリアの制圧、強敵(Boss)を倒すとミッション達成。</summary>
public enum EnemyKind
{
    /// <summary>敵エリアに最初からいる雑魚。倒すとエリア制圧=設置スロット解放。</summary>
    Mob,

    /// <summary>出現ポイントをアトラクトすると現れる強敵。プレイヤーを追跡する。</summary>
    Boss,
}
