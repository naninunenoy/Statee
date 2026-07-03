namespace Statee.Core;

/// <summary>
/// <see cref="StateeStateAttribute"/> を付与した型の中で、State スナップショットに
/// 含めるプロパティ・フィールドに付与する(docs/MEMO.md D-022)。
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, Inherited = false)]
public sealed class StateeFieldAttribute : Attribute;
