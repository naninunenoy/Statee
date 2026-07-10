namespace Statee.Core;

/// <summary>
/// デリゲートでスナップショットを返す State プロバイダ。
/// CaptureState はソケットスレッドから呼ばれるため、capture は不変スナップショットを
/// 返すだけにする(可変状態を読み歩かない)。
/// </summary>
public sealed class SnapshotStateProvider(string path, Func<object> capture) : IStateProvider
{
    public string Path => path;

    public object CaptureState() => capture();
}
