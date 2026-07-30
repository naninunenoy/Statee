using System.Numerics;

namespace TpsGame.Logic;

/// <summary>
/// ゲームルールの定数。論理座標系は Y 上向きの 3D(XZ 平面が地面)。
/// 速度はすべて「1 Tick(1/60 秒)あたりのワールド単位」で表す。
/// </summary>
public sealed record TpsConfig
{
    /// <summary>アリーナ半幅(X)。プレイヤーと弾はこの範囲にクランプ/消滅する。</summary>
    public float ArenaHalfExtent { get; init; } = 20f;

    /// <summary>プレイヤーの移動速度(単位/Tick)。</summary>
    public float PlayerSpeed { get; init; } = 0.12f;

    /// <summary>プレイヤーの当たり判定半径(XZ)。</summary>
    public float PlayerRadius { get; init; } = 0.4f;

    /// <summary>プレイヤーの初期位置。</summary>
    public Vector3 PlayerStart { get; init; } = new(0f, 0f, 0f);

    /// <summary>プレイヤーの初期向き(ラジアン。0 で +Z 正面)。</summary>
    public float PlayerStartYaw { get; init; } = 0f;

    /// <summary>弾の速度(単位/Tick)。</summary>
    public float BulletSpeed { get; init; } = 0.5f;

    /// <summary>弾の当たり判定半径。</summary>
    public float BulletRadius { get; init; } = 0.15f;

    /// <summary>弾の発射高さ(Y)。人型モデルの胴付近。</summary>
    public float BulletHeight { get; init; } = 1.0f;

    /// <summary>射撃押しっぱなし時の発射間隔(Tick)。</summary>
    public int FireIntervalTicks { get; init; } = 12;

    /// <summary>的の当たり判定半径(XZ + 高さは円柱近似)。</summary>
    public float TargetRadius { get; init; } = 0.55f;

    /// <summary>的の高さ(円柱の上端 Y)。</summary>
    public float TargetHeight { get; init; } = 2.0f;

    /// <summary>的を破壊してから復活するまでの Tick 数。</summary>
    public int TargetRespawnTicks { get; init; } = 180;

    /// <summary>的の初期配置(ワールド座標。Y は足元)。</summary>
    public IReadOnlyList<Vector3> TargetSpawns { get; init; } =
    [
        new(6f, 0f, 8f),
        new(-6f, 0f, 8f),
        new(0f, 0f, 12f),
        new(8f, 0f, -4f),
        new(-8f, 0f, -4f),
    ];
}
