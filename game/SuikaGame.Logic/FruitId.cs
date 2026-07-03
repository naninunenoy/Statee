using UnitGenerator;

namespace SuikaGame.Logic;

/// <summary>
/// 場に出たフルーツの安定 ID(docs/MEMO.md D-006)。
/// AI がフレームを跨いで同一フルーツを追跡するために、合体で消えるまで変わらない。
/// </summary>
[UnitOf(typeof(int))]
public readonly partial struct FruitId;
