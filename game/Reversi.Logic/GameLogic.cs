namespace Reversi.Logic;

/// <summary>
/// ゲームの中核状態機械のプレースホルダ。ルール・状態遷移はすべてこの層に置き、
/// Godot 層には持ち込まない(docs/USING.md「境界の掟」)。
/// 乱数を使う場合はこのシードだけを源にし、決定論を保つ。
/// </summary>
public sealed class GameLogic(int seed)
{
    /// <summary>生成に使ったシード。State で公開して再現性を検証できるようにする。</summary>
    public int Seed { get; } = seed;

    /// <summary>進めたターン数(プレースホルダ)。</summary>
    public int StepCount { get; private set; }

    /// <summary>1ターン進める(プレースホルダ)。実ゲームのアクションに置き換える。</summary>
    public void Step()
    {
        StepCount++;
    }
}
