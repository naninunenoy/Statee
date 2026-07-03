namespace Statee.Core;

/// <summary>
/// State を公開する型に付与する(docs/MEMO.md D-022)。
/// partial class に付与すると、ソースジェネレータが <see cref="IStateProvider"/> 実装を生成する。
/// 公開するメンバーには <see cref="StateeFieldAttribute"/> を付与する。
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class StateeStateAttribute(string path) : Attribute
{
    /// <summary>State のパス(例: "system/runtime")。</summary>
    public string Path { get; } = path;
}
