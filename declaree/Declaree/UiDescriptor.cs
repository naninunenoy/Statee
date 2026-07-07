namespace Declaree;

/// <summary>
/// UiNode ツリーの型非依存な平坦表現。TOON 化(Statee の State 公開)や
/// 外部フロントエンドとの境界で使う。
/// </summary>
public record UiDescriptor(
    string Type,
    IReadOnlyDictionary<string, string> Props,
    IReadOnlyList<UiDescriptor> Children
);
