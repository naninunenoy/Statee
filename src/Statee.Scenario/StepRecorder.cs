namespace Statee.Scenario;

/// <summary>IStepRecorder の標準実装。メモリ上にステップを蓄積する。</summary>
public sealed class StepRecorder : IStepRecorder
{
    public string? CurrentExpectation => throw new NotImplementedException();

    public IReadOnlyList<ScenarioStep> Steps => throw new NotImplementedException();

    public void BeginExpectation(string description) => throw new NotImplementedException();

    public void Record(ScenarioStep step) => throw new NotImplementedException();
}
