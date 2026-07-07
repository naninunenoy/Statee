namespace Declaree;

/// <summary>子の四辺に等幅の余白(px)を取るコンテナ。Godot の MarginContainer に対応する。</summary>
public record Margin(int All, UiNode Child) : UiNode;
