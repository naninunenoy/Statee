namespace Statee.Scenario;

/// <summary>
/// レポートに載せる1ステップの記録(D-034)。send / wait の実行結果と、
/// その直後に取得したスクリーンショット・State を束ねる。
/// </summary>
/// <param name="Command">送ったコマンド名。</param>
/// <param name="Args">コマンド引数(なければ null)。</param>
/// <param name="Payload">成功時の payload(TOON)。失敗時は null。</param>
/// <param name="Expectation">このステップが属する期待セクションの説明文(expect 未記載なら null)。</param>
/// <param name="ScreenshotPath">保存されたスクリーンショットの絶対パス(取得できなければ null)。</param>
/// <param name="StateToon">ステップ直後に取得した State の TOON(取得していなければ null)。</param>
/// <param name="Error">ステップが失敗した場合のエラーメッセージ。</param>
public sealed record ScenarioStep(
    string Command,
    IReadOnlyDictionary<string, string>? Args,
    string? Payload,
    string? Expectation,
    string? ScreenshotPath,
    string? StateToon,
    string? Error
);
