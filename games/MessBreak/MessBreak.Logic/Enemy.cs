using System.Numerics;

namespace MessBreak.Logic;

/// <summary>
/// 敵エンティティ。デバフ(被ダメージ増幅)は敵ごとに独立して持つ。
/// 書き換えはロジック内のみ(internal set)。HP 0 になった敵はリストから取り除かれる。
/// </summary>
public sealed class Enemy
{
    public required int Id { get; init; }
    public required EnemyKind Kind { get; init; }
    public Vector2 Pos { get; internal set; }
    public int Hp { get; internal set; }

    /// <summary>デバフ(被ダメージ増幅)の残り tick 数(0 なら未付与)。</summary>
    public int DebuffTicks { get; internal set; }

    /// <summary>付与中デバフの倍率(付与したデバッファーの設定値を保持)。</summary>
    internal int DebuffMultiplier { get; set; } = 1;
}
