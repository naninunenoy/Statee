using System;

namespace Declaree.Godot;

/// <summary>
/// UiNode ツリーを Godot の Control ノードに変換する。
/// 全破棄・全再構築方式(D-035)。差分適用は必要が証明されてから導入する。
/// </summary>
public static class UiRenderer
{
    /// <summary>
    /// ツリーから Control を生成する。Button 押下時は <paramref name="dispatch"/> に
    /// イベント ID(<see cref="Declaree.Button.OnClick"/>)が渡る。
    /// 返されたノードの解放は呼び出し側の責任(再構築時は QueueFree)。
    /// </summary>
    public static global::Godot.Control Render(UiNode node, Action<string> dispatch) => default!;
}
