using System.Numerics;

namespace MessBreak.Logic;

/// <summary>
/// インタラクト(F)で操作できる設置物・仕掛けの抽象。タレットスロットや強敵の出現ポイント
/// のように「場所に紐づき、条件を満たすとき一度だけ反応する」ものをここに載せる。
/// 発動の選択規則は BattleLogic 側で共通(範囲内で発動可能な最寄りの1つ)。
/// </summary>
public abstract class Interactable
{
    /// <summary>設置場所(ワールド座標)。</summary>
    public abstract Vector2 Pos { get; }

    /// <summary>インタラクトが届く、プレイヤーとの最大距離。</summary>
    public abstract float Range { get; }

    /// <summary>いま発動できるか(解放条件・使用済みなどの判定)。</summary>
    public abstract bool CanInteract { get; }

    /// <summary>発動する(ロジック内からのみ)。</summary>
    internal abstract void Interact();
}
