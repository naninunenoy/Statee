using System.Reflection;

namespace Statee.Core;

/// <summary>
/// 接続先プロセスの同一性確認用 State(system/identity)。
/// 「古いバイナリ・別プロセスに繋いでいた」事故を、検証の冒頭で機械的に検出するための入口
/// (docs/adr/D-075)。anchor には対象アプリ自身のアセンブリ(例: ゲームの Main の Assembly)を渡す。
/// 値は起動時に固定される不変スナップショットで、どのスレッドから読んでも安全。
/// </summary>
public sealed class IdentityStateProvider(Assembly anchor) : IStateProvider
{
    /// <summary>
    /// 接続先確認に使う情報のひとまとまり。時刻は ISO 8601(ローカル時刻)。
    /// Mvid はビルドごとに一意で、「同じソースでも別ビルド」を確実に区別できる。
    /// AssemblyPath / AssemblyBuiltAt はベストエフォート(Godot mono はストリームロードのため
    /// Location が空になり、その場合は空文字)。
    /// </summary>
    public sealed record Snapshot(
        int Pid,
        string StartedAt,
        string AssemblyName,
        string Mvid,
        string AssemblyPath,
        string AssemblyBuiltAt
    );

    private readonly Snapshot _snapshot = new(
        Environment.ProcessId,
        System.Diagnostics.Process.GetCurrentProcess().StartTime.ToString("O"),
        anchor.GetName().Name ?? "",
        anchor.ManifestModule.ModuleVersionId.ToString(),
        anchor.Location,
        anchor.Location.Length == 0 ? "" : File.GetLastWriteTime(anchor.Location).ToString("O")
    );

    public string Path => "system/identity";

    public object CaptureState() => _snapshot;
}
