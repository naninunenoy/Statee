namespace Statee.Scenario;

/// <summary>IStepRecorder の標準実装。メモリ上にステップを蓄積する。</summary>
public sealed class StepRecorder : IStepRecorder
{
    private readonly List<ScenarioStep> _steps = [];

    public string? CurrentExpectation { get; private set; }

    public IReadOnlyList<ScenarioStep> Steps => _steps;

    public void BeginExpectation(string description) => CurrentExpectation = description;

    public void Record(ScenarioStep step) => _steps.Add(step);
}
