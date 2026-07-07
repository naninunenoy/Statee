namespace Declaree;

/// <summary>子を横に並べるコンテナ。</summary>
public record HBox(params UiNode[] Children) : UiNode;
