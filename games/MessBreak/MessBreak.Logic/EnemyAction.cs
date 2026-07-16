namespace MessBreak.Logic;

/// <summary>敵の行動状態(FSM)。Idle → Chase → Windup → Recovery → Chase … と遷移する。</summary>
public enum EnemyAction
{
    Idle,
    Chase,
    Windup,
    Recovery,
    Dead,
}
