using Statee.Core;

namespace Declaree.Statee;

/// <summary>
/// UI の記述子スナップショットを Statee の State として公開する薄いアダプタ(D-035)。
/// AI Agent が画面内容(幾何 Rect 込み)を構造化データとして観測できるようにする。
/// CaptureState はソケットスレッドから呼ばれるため、<c>getSnapshot</c> は
/// メインスレッドが差し替えた不変スナップショットへの参照をアトミックに返すこと
/// (幾何は UiSnapshot.Capture がメインスレッドで採取する)。
/// </summary>
public class UiStateProvider(string path, Func<UiDescriptor> getSnapshot) : IStateProvider
{
    private readonly string path = path;
    private readonly Func<UiDescriptor> getSnapshot = getSnapshot;

    public string Path => default!;

    public object CaptureState() => default!;
}
