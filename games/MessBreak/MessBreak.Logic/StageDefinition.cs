using System.Numerics;

namespace MessBreak.Logic;

/// <summary>
/// ステージ(ダンジョン)の定義。形状は正方形セルのグリッド(行文字列の '#' が壁、その他は床)、
/// 敵・設置物の配置はワールド座標で持つ。グリッド範囲外は壁とみなすため、外周を壁で囲まなくても
/// プレイヤーがステージ外へ出ることはない。
/// テキスト形式からの生成は <see cref="StageMap.Parse"/>。テスト・手続き生成からは直接構築する。
/// </summary>
public sealed record StageDefinition
{
    /// <summary>セル1辺のワールド単位長。</summary>
    public float TileSize { get; init; } = 40f;

    /// <summary>形状。行ごとの文字列で、'#' のセルだけが壁。全行同じ長さであること。</summary>
    public required IReadOnlyList<string> Rows { get; init; }

    public required Vector2 PlayerSpawn { get; init; }

    /// <summary>雑魚の初期配置。全滅させるとエリア制圧になる。</summary>
    public IReadOnlyList<Vector2> MobSpawns { get; init; } = [];

    /// <summary>強敵の出現ポイント(アトラクトの対象)。</summary>
    public required Vector2 BossSpawn { get; init; }

    /// <summary>タレットの設置スロット。</summary>
    public required Vector2 TurretSlot { get; init; }

    /// <summary>ステージ全体のワールド幅。</summary>
    public float Width => default;

    /// <summary>ステージ全体のワールド高さ。</summary>
    public float Height => default;

    /// <summary>指定セルが壁か。グリッド範囲外は壁とみなす。</summary>
    public bool IsSolidCell(int col, int row) => default;

    /// <summary>指定ワールド座標(点)が壁の中か。</summary>
    public bool IsSolidAt(Vector2 pos) => default;

    /// <summary>指定の円が壁セルに重なるか(接しているだけなら false)。</summary>
    public bool OverlapsSolid(Vector2 center, float radius) => default;
}
