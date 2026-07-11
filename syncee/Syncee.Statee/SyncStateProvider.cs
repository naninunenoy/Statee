using Statee.Core;

namespace Syncee.Statee;

/// <summary>
/// 同期状態(<see cref="SyncSnapshot"/>)を Statee の State として公開する薄いアダプタ。
/// Declaree.Statee の UiStateProvider と同じ流儀(D-035)。
/// </summary>
public class SyncStateProvider(string path, Func<SyncSnapshot> getSnapshot) : IStateProvider
{
    private readonly string path = path;
    private readonly Func<SyncSnapshot> getSnapshot = getSnapshot;

    public string Path => path;

    public object CaptureState() => getSnapshot();
}
