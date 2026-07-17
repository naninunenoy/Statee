namespace MessBreak.Logic;

/// <summary>操作キャラクター。切り替え=スキルセットの差し替え(docs/DESIGN.md)。</summary>
public enum CharacterId
{
    /// <summary>アタッカー: 範囲ダメージのスキル。</summary>
    Attacker,

    /// <summary>デバッファー: 被ダメージを増幅するデバフを付与するスキル。</summary>
    Debuffer,
}
