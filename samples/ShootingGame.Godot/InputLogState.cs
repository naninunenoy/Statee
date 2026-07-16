using System.Collections.Generic;
using Statee.Core;

namespace ShootingGame;

/// <summary>
/// 受け付けた全入力のランレングス記録を公開する(D-048 の検証の柱: フレーム精度リプレイ)。
/// 各要素は「120 right+shoot」「30 -」(- は無入力)の形式で、そのまま
/// tick コマンド(--arg frames=N,input=...)として同一シードの起動に再生できる。
/// CaptureState はソケットスレッドで走るため、メインスレッドが差し替える
/// 不変スナップショットを読むだけにする。
/// </summary>
[StateeState("game/shootinggame/inputs")]
public partial class InputLogState
{
    private volatile IReadOnlyList<string> _runs = [];

    [StateeField]
    public IReadOnlyList<string> Runs => _runs;

    /// <summary>メインスレッドから呼ぶ。スナップショットを不可分に差し替える。</summary>
    public void Update(IReadOnlyList<string> runs)
    {
        _runs = runs;
    }
}
