namespace Declaree;

/// <summary>
/// ボタン。押下時の振る舞いはクロージャではなくイベント ID(<paramref name="OnClick"/>)で表し、
/// IR のシリアライズ可能性を守る(docs/adr/D-035.md)。
/// </summary>
public record Button(string Text, string OnClick) : UiNode
{
    /// <summary>無効化。true のとき押下できず、イベントも発行されない。</summary>
    public bool Disabled { get; init; }
}
