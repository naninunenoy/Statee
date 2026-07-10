using System.Collections.Generic;
using Statee.Core;

namespace RogueGame;

/// <summary>
/// 受け付けた全アクションの記録を公開する(D-044 の検証の柱3: リプレイ検証)。
/// 各要素は「move west」「use potion」の形式で、そのまま move / use コマンドの
/// 引数として再生できる。CaptureState はソケットスレッドで走るため(D-019)、
/// メインスレッドが差し替える不変スナップショットを読むだけにする。
/// </summary>
[StateeState("game/rogue/actions")]
public partial class RogueActionLogState
{
    private volatile IReadOnlyList<string> _actions = [];

    [StateeField]
    public IReadOnlyList<string> Actions => _actions;

    /// <summary>メインスレッドから呼ぶ。スナップショットを不可分に差し替える。</summary>
    public void Update(IReadOnlyList<string> actions)
    {
        _actions = actions;
    }
}
