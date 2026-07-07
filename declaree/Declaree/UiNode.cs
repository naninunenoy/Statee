namespace Declaree;

/// <summary>
/// 宣言的 UI の中間表現(IR)。Godot 非依存のツリーで、
/// UI 定義はこのツリーを返す純粋な C# 式として書く(docs/adr/D-035.md)。
/// </summary>
public abstract record UiNode;
