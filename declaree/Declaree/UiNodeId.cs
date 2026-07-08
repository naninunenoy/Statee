using System.Globalization;
using UnitGenerator;

namespace Declaree;

/// <summary>
/// UI 要素のツリー位置由来の安定 ID(ルート "0"、子は "0.1.2" 形式)。
/// 同じツリー形状なら再構築・再実行を跨いで同じ値になる(決定論。GUID は使わない)。
/// name(D-038)が意味ベースの任意指定であるのに対し、id は全要素に必ず付く。
/// </summary>
[UnitOf(typeof(string))]
public readonly partial struct UiNodeId
{
    /// <summary>ルート要素の ID。</summary>
    public static UiNodeId Root => new("0");

    /// <summary>この要素の index 番目の子の ID を導出する。</summary>
    public UiNodeId Child(int index) =>
        new(string.Create(CultureInfo.InvariantCulture, $"{value}.{index}"));
}
