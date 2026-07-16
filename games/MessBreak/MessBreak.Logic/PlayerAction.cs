namespace MessBreak.Logic;

/// <summary>プレイヤーの行動状態。Free 以外の間は移動・新規アクションを受け付けない。</summary>
public enum PlayerAction
{
    Free,
    Attack,
    Dodge,
    Dead,
}
