namespace Declaree;

/// <summary>子を親領域の中央に配置するコンテナ(Godot では CenterContainer)。</summary>
public record Center(UiNode Child) : UiNode;
