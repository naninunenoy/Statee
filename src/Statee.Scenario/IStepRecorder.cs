namespace Statee.Scenario;

/// <summary>
/// シナリオ実行中のステップ記録の受け口(D-034)。
/// expect 語彙が BeginExpectation を呼び、RecordingScenarioClient が Record を呼ぶ。
/// </summary>
public interface IStepRecorder
{
    /// <summary>新しい期待セクションを開始する。以降の Record はこの説明文に紐づく。</summary>
    void BeginExpectation(string description);

    /// <summary>現在の期待セクションの説明文。expect 未実行なら null。</summary>
    string? CurrentExpectation { get; }

    /// <summary>1ステップを記録する。</summary>
    void Record(ScenarioStep step);

    /// <summary>記録済みステップ(記録順)。</summary>
    IReadOnlyList<ScenarioStep> Steps { get; }
}
