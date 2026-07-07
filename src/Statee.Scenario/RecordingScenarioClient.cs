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
    private readonly string _shotsDir;
    private readonly string? _statePath;
    private int _stepNumber;

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
        // ターゲットプロセスの CWD に依存しないよう、送るパスは常に絶対パスへ解決する(D-034)
        _shotsDir = Path.GetFullPath(Path.Combine(reportDir, "shots"));
        _statePath = statePath;
    }

    public string Invoke(string command, IReadOnlyDictionary<string, string>? args = null)
    {
        if (command is "state" or "screenshot")
        {
            return _inner.Invoke(command, args);
        }

        string payload;
        try
        {
            payload = _inner.Invoke(command, args);
        }
        catch (Exception e)
        {
            _recorder.Record(NewStep(command, args) with { Error = e.Message });
            throw;
        }

        var step = NewStep(command, args) with { Payload = payload };
        _stepNumber++;
        var shotPath = Path.Combine(_shotsDir, $"step-{_stepNumber:000}.png");
        // 記録の失敗でシナリオ本体を止めない。失敗は Error としてレポートに残す
        try
        {
            _inner.Invoke("screenshot", new Dictionary<string, string> { ["path"] = shotPath });
            step = step with { ScreenshotPath = shotPath };
        }
        catch (Exception e)
        {
            step = step with { Error = $"screenshot 失敗: {e.Message}" };
        }

        if (_statePath is not null)
        {
            try
            {
                var toon = _inner.Invoke(
                    "state",
                    new Dictionary<string, string> { ["path"] = _statePath }
                );
                step = step with { StateToon = toon };
            }
            catch (Exception e)
            {
                var error = $"state 取得失敗: {e.Message}";
                // screenshot 失敗のエラーを上書きせず併記する
                step = step with
                {
                    Error = step.Error is null ? error : $"{step.Error} / {error}",
                };
            }
        }

        _recorder.Record(step);
        return payload;
    }

    private ScenarioStep NewStep(string command, IReadOnlyDictionary<string, string>? args) =>
        new(
            command,
            args,
            Payload: null,
            _recorder.CurrentExpectation,
            ScreenshotPath: null,
            StateToon: null,
            Error: null
        );
}
