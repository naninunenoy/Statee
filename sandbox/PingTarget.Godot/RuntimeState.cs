using System;
using System.Diagnostics;
using System.Threading;
using Statee.Core;

namespace PingTarget;

/// <summary>
/// 可変の system State(docs/adr/D-019.md)。
/// IStateProvider 実装はソースジェネレータが生成する(D-022)。
/// ソケットスレッドから読まれるため、可変値は Interlocked / Stopwatch でスレッド安全にする。
/// </summary>
[StateeState("system/runtime")]
public partial class RuntimeState
{
    private readonly Stopwatch _uptime = Stopwatch.StartNew();
    private long _frame;

    public void IncrementFrame() => Interlocked.Increment(ref _frame);

    [StateeField]
    public long Frame => Interlocked.Read(ref _frame);

    [StateeField]
    public double UptimeSeconds => Math.Round(_uptime.Elapsed.TotalSeconds, 3);
}
