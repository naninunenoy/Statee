namespace Statee.Scenario;

/// <summary>
/// IScenarioClient のデコレータ(D-034)。send / wait の成功直後に
/// screenshot と state を追加送信し、ステップとして recorder に記録する。
/// state / screenshot 自身のステップは素通しし記録しない。
/// </summary>
public sealed class RecordingScenarioClient : IScenarioClient
{
    private readonly IScenarioClient _inner;
    private readonly IStepRecorder _recorder;
    private readonly string _reportDir;
    private readonly string? _statePath;

    /// <param name="inner">実際にターゲットへ送るクライアント。</param>
    /// <param name="recorder">記録先。</param>
    /// <param name="reportDir">レポート出力ディレクトリ。スクショは shots/ 配下に絶対パスで保存させる。</param>
    /// <param name="statePath">ステップ直後に取得する State のパス。null なら State は取得しない。</param>
    public RecordingScenarioClient(
        IScenarioClient inner,
        IStepRecorder recorder,
        string reportDir,
        string? statePath = null
    )
    {
        _inner = inner;
        _recorder = recorder;
        _reportDir = reportDir;
        _statePath = statePath;
    }

    public string Invoke(string command, IReadOnlyDictionary<string, string>? args = null) =>
        throw new NotImplementedException();
}
