using Microsoft.CodeAnalysis;

namespace Statee.Generator;

/// <summary>
/// [StateeState] を付与した partial class に IStateProvider 実装を生成する
/// incremental generator(docs/MEMO.md D-022)。
/// </summary>
[Generator]
public sealed class StateProviderGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // スケルトン。実装はテストファーストで行う(docs/GUIDELINE.md §6)
    }
}
