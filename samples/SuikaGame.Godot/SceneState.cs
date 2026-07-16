using Statee.Core;

namespace SuikaGame;

/// <summary>
/// 画面フェーズの State 公開。CaptureState はソケットスレッドで走るため(D-019)、
/// メインスレッドが遷移時に差し替える値を読むだけにする。
/// </summary>
[StateeState("game/scene")]
public partial class SceneState
{
    private volatile string _phase = "";

    [StateeField]
    public string Phase => _phase;

    /// <summary>メインスレッドから呼ぶ。</summary>
    public void Update(string phase)
    {
        _phase = phase;
    }
}
