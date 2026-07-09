namespace RogueGame.Logic;

/// <summary>アイテムの種類。</summary>
public enum ItemKind
{
    /// <summary>🧪 ポーション。使うと HP が回復する。インベントリに入る。</summary>
    Potion,

    /// <summary>🗡️ 剣。拾った瞬間に攻撃力が上がる(インベントリには入らない)。</summary>
    Sword,
}
