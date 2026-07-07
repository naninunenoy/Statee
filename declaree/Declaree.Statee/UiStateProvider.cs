using Statee.Core;

namespace Declaree.Statee;

/// <summary>
/// 現在の UiNode ツリーを Statee の State として公開する薄いアダプタ(D-035)。
/// AI Agent が画面内容を構造化データとして観測できるようにする。
/// CaptureState はソケットスレッドから呼ばれるため、<c>getTree</c> は
/// その時点のツリー(不変 record)への参照をアトミックに返すこと。
/// </summary>
public class UiStateProvider(string path, Func<UiNode?> getTree) : IStateProvider
{
    private readonly string path = path;
    private readonly Func<UiNode?> getTree = getTree;

    public string Path => default!;

    public object CaptureState() => default!;
}
