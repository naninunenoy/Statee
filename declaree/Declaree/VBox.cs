namespace Declaree;

/// <summary>子を縦に並べるコンテナ。</summary>
public record VBox(params UiNode[] Children) : UiNode;
