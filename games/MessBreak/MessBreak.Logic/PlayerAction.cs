namespace MessBreak.Logic;

/// <summary>プレイヤーの行動状態。射撃は行動状態を持たず(移動と両立する)、クールダウンのみで律速する。</summary>
public enum PlayerAction
{
    Free,
    Dodge,
    Dead,
}
