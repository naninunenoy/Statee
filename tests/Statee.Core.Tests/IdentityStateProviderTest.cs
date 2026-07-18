using Shouldly;

namespace Statee.Core.Tests;

/// <summary>
/// system/identity(接続先プロセスの同一性確認用 State)の仕様。
/// 「古いバイナリ・別プロセスに繋いでいた」事故を機械的に検出するための情報を公開する
/// (docs/adr/D-075)。
/// </summary>
public class IdentityStateProviderTest
{
    private static IdentityStateProvider Create() =>
        new(typeof(IdentityStateProviderTest).Assembly);

    private static IdentityStateProvider.Snapshot Capture() =>
        (IdentityStateProvider.Snapshot)Create().CaptureState();

    [Fact]
    public void Path_は_system_identity()
    {
        Create().Path.ShouldBe("system/identity");
    }

    [Fact]
    public void CaptureState_現在プロセスのPidを含む()
    {
        Capture().Pid.ShouldBe(Environment.ProcessId);
    }

    [Fact]
    public void CaptureState_起動時刻とアセンブリ情報を含む()
    {
        var state = Capture();

        state.StartedAt.ShouldNotBeNullOrEmpty();
        state.AssemblyName.ShouldBe("Statee.Core.Tests");
        state.Mvid.ShouldNotBeNullOrEmpty();
        state.AssemblyPath.ShouldEndWith("Statee.Core.Tests.dll");
        state.AssemblyBuiltAt.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void CaptureState_Location情報が無いアセンブリでも落ちない()
    {
        // Godot mono はストリームロードのため Location が空になる。その状況の代替として
        // 動的アセンブリ(Location が空)でも構築できることを確認する
        var dynamicAssembly = System.Reflection.Emit.AssemblyBuilder.DefineDynamicAssembly(
            new System.Reflection.AssemblyName("Statee.DynamicAnchor"),
            System.Reflection.Emit.AssemblyBuilderAccess.Run
        );

        var state = (IdentityStateProvider.Snapshot)
            new IdentityStateProvider(dynamicAssembly).CaptureState();

        state.AssemblyName.ShouldBe("Statee.DynamicAnchor");
        state.AssemblyPath.ShouldBe("");
        state.AssemblyBuiltAt.ShouldBe("");
    }

    [Fact]
    public void CaptureState_毎回同じスナップショットを返す()
    {
        var provider = Create();

        provider.CaptureState().ShouldBeSameAs(provider.CaptureState());
    }

    [Fact]
    public void StateeHost_に登録すると_system_identity_で取得できる()
    {
        var host = new StateeHost(new LogBuffer(8));
        host.RegisterStateProvider(Create());

        var state = (IdentityStateProvider.Snapshot)host.CaptureState("system/identity");

        state.Pid.ShouldBe(Environment.ProcessId);
    }
}
