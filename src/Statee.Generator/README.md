# Statee.Generator

`[StateeState]` を付与した partial class に `IStateProvider` 実装を生成する
Roslyn incremental generator(docs/adr/D-022.md)。

```csharp
[StateeState("system/runtime")]
public partial class RuntimeState
{
    [StateeField]
    public long Frame { get; set; }
}
```

これで `Path` と、`[StateeField]` 付きメンバーの現在値を匿名型で返す
`CaptureState()` が生成され、`StateeHost.RegisterStateProvider` にそのまま渡せる。
partial 修飾子が無い場合はエラー診断 `STATEE001` を報告する。

利用側 csproj には Analyzer として参照を追加する:

```xml
<ProjectReference Include="..\..\src\Statee.Generator\Statee.Generator.csproj"
                  OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
```

テストは `tests/Statee.Generator.Tests`(GeneratorDriver で生成し、
インメモリコンパイル・ロードして振る舞いを検証)。
