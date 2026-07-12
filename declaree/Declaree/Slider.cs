namespace Declaree;

/// <summary>
/// 水平スライダー。値の変更確定時(ドラッグ終了・キー操作)にイベント ID(<paramref name="OnChange"/>)を
/// dispatch する。値そのものはイベントに載せず、ホストが Name(D-038)で Godot コントロールを
/// 直接参照して読む(LineEdit と同じ方針。D-035)。
/// </summary>
public record Slider(double Min, double Max, double Value, string OnChange) : UiNode
{
    /// <summary>値の刻み幅。</summary>
    public double Step { get; init; } = 1;
}
