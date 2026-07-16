using UnitGenerator;

namespace RogueGame.Logic;

/// <summary>
/// 敵の安定 ID(docs/adr/D-006.md)。
/// AI がフレームを跨いで同一の敵を追跡するために、倒されるまで変わらない。
/// </summary>
[UnitOf(typeof(int))]
public readonly partial struct EnemyId;
