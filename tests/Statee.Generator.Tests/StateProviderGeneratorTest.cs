using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Shouldly;
using Statee.Core;

namespace Statee.Generator.Tests;

public class StateProviderGeneratorTest
{
    [Fact]
    public void Generate_StateeState付きのpartialクラス_IStateProvider実装が生成されPathが返る()
    {
        const string source = """
            using Statee.Core;

            namespace Target;

            [StateeState("system/runtime")]
            public partial class RuntimeState
            {
                [StateeField]
                public long Frame { get; set; }
            }
            """;

        var (compilation, _) = RunGenerator(source);

        var provider = CreateProviderInstance(compilation, "Target.RuntimeState");
        provider.Path.ShouldBe("system/runtime");
    }

    [Fact]
    public void Generate_StateeField付きのプロパティとフィールド_スナップショットに現在値が含まれる()
    {
        const string source = """
            using Statee.Core;

            namespace Target;

            [StateeState("system/runtime")]
            public partial class RuntimeState
            {
                [StateeField]
                public long Frame { get; set; }

                [StateeField]
                public double UptimeSeconds;
            }
            """;

        var (compilation, _) = RunGenerator(source);

        var provider = CreateProviderInstance(compilation, "Target.RuntimeState");
        SetMember(provider, "Frame", 42L);
        SetMember(provider, "UptimeSeconds", 1.5);
        var snapshot = provider.CaptureState();
        GetSnapshotValue(snapshot, "Frame").ShouldBe(42L);
        GetSnapshotValue(snapshot, "UptimeSeconds").ShouldBe(1.5);
    }

    [Fact]
    public void Generate_StateeFieldの無いメンバー_スナップショットに含まれない()
    {
        const string source = """
            using Statee.Core;

            namespace Target;

            [StateeState("system/runtime")]
            public partial class RuntimeState
            {
                [StateeField]
                public long Frame { get; set; }

                public string Secret { get; set; } = "hidden";
            }
            """;

        var (compilation, _) = RunGenerator(source);

        var provider = CreateProviderInstance(compilation, "Target.RuntimeState");
        var snapshot = provider.CaptureState();
        snapshot.GetType().GetProperty("Secret").ShouldBeNull();
    }

    [Fact]
    public void Generate_partialでないクラス_エラー診断STATEE001を報告する()
    {
        const string source = """
            using Statee.Core;

            namespace Target;

            [StateeState("system/runtime")]
            public class RuntimeState
            {
                [StateeField]
                public long Frame { get; set; }
            }
            """;

        var (_, diagnostics) = RunGenerator(source);

        diagnostics.ShouldContain(d =>
            d.Id == "STATEE001" && d.Severity == DiagnosticSeverity.Error
        );
    }

    [Fact]
    public void Generate_ネームスペース無しのクラス_生成コードがコンパイルできPathが返る()
    {
        const string source = """
            using Statee.Core;

            [StateeState("system/global")]
            public partial class GlobalState
            {
                [StateeField]
                public int Value { get; set; }
            }
            """;

        var (compilation, _) = RunGenerator(source);

        var provider = CreateProviderInstance(compilation, "GlobalState");
        provider.Path.ShouldBe("system/global");
    }

    private static (Compilation Compilation, ImmutableArray<Diagnostic> Diagnostics) RunGenerator(
        string source
    )
    {
        var references = AppDomain
            .CurrentDomain.GetAssemblies()
            .Where(assembly => !assembly.IsDynamic && !string.IsNullOrEmpty(assembly.Location))
            .Select(assembly =>
                (MetadataReference)MetadataReference.CreateFromFile(assembly.Location)
            )
            .Append(MetadataReference.CreateFromFile(typeof(IStateProvider).Assembly.Location))
            .Distinct()
            .ToList();
        var compilation = CSharpCompilation.Create(
            "GeneratorTarget",
            [CSharpSyntaxTree.ParseText(source)],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );
        var driver = CSharpGeneratorDriver.Create(new StateProviderGenerator());
        driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var outputCompilation,
            out var diagnostics
        );
        return (outputCompilation, diagnostics);
    }

    /// <summary>生成コード込みでコンパイル・ロードし、IStateProvider として取り出す。</summary>
    private static IStateProvider CreateProviderInstance(Compilation compilation, string typeName)
    {
        using var stream = new MemoryStream();
        var emitResult = compilation.Emit(stream);
        emitResult.Success.ShouldBeTrue(string.Join(Environment.NewLine, emitResult.Diagnostics));
        var assembly = Assembly.Load(stream.ToArray());
        var type = assembly.GetType(typeName);
        type.ShouldNotBeNull();
        var instance = Activator.CreateInstance(type)!;
        return instance.ShouldBeAssignableTo<IStateProvider>()!;
    }

    private static void SetMember(object instance, string name, object value)
    {
        var type = instance.GetType();
        var property = type.GetProperty(name);
        if (property is not null)
        {
            property.SetValue(instance, value);
            return;
        }

        type.GetField(name)!.SetValue(instance, value);
    }

    private static object? GetSnapshotValue(object snapshot, string name)
    {
        var property = snapshot.GetType().GetProperty(name);
        property.ShouldNotBeNull($"スナップショットに {name} が無い");
        return property.GetValue(snapshot);
    }
}
