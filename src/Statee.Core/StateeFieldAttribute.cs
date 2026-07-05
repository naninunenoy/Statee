namespace Statee.Core;

/// <summary>
/// <see cref="StateeStateAttribute"/> を付与した型の中で、State スナップショットに
/// 含めるプロパティ・フィールドに付与する(docs/adr/D-022.md)。
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, Inherited = false)]
public sealed class StateeFieldAttribute : Attribute;
