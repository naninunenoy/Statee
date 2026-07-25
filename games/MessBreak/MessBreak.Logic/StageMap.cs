using System.Numerics;

namespace MessBreak.Logic;

/// <summary>
/// ステージのテキスト形式(ASCII マップ)。1文字=1セルで、ダンジョンの形状と配置を定義する:
/// <code>
/// '#' = 壁   '.' = 床
/// 'P' = プレイヤー初期位置(必須・1つ)
/// 'M' = 雑魚(0個以上。全滅でエリア制圧)
/// 'B' = 強敵の出現ポイント(必須・1つ)
/// 'T' = タレットの設置スロット(必須・1つ)
/// </code>
/// マーカーのセルは床扱いで、位置はセル中心のワールド座標になる。
/// </summary>
public static class StageMap
{
    /// <summary>
    /// テキストを <see cref="StageDefinition"/> にパースする。
    /// 行長の不揃い・未知の文字・必須マーカーの過不足は <see cref="StageMapException"/>。
    /// </summary>
    public static StageDefinition Parse(string text, float tileSize = 40f)
    {
        var rows = text.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.TrimEnd('\r'))
            .ToArray();
        if (rows.Length == 0)
        {
            throw new StageMapException("マップが空です");
        }

        Vector2? playerSpawn = null;
        Vector2? bossSpawn = null;
        Vector2? turretSlot = null;
        var mobSpawns = new List<Vector2>();
        for (var row = 0; row < rows.Length; row++)
        {
            if (rows[row].Length != rows[0].Length)
            {
                throw new StageMapException($"行 {row} の長さが揃っていません: '{rows[row]}'");
            }
            for (var col = 0; col < rows[row].Length; col++)
            {
                var center = new Vector2((col + 0.5f) * tileSize, (row + 0.5f) * tileSize);
                switch (rows[row][col])
                {
                    case '#' or '.':
                        break;
                    case 'P':
                        playerSpawn = playerSpawn is null
                            ? center
                            : throw new StageMapException("P(プレイヤー初期位置)が複数あります");
                        break;
                    case 'M':
                        mobSpawns.Add(center);
                        break;
                    case 'B':
                        bossSpawn = bossSpawn is null
                            ? center
                            : throw new StageMapException("B(強敵の出現ポイント)が複数あります");
                        break;
                    case 'T':
                        turretSlot = turretSlot is null
                            ? center
                            : throw new StageMapException("T(タレットの設置スロット)が複数あります");
                        break;
                    default:
                        throw new StageMapException(
                            $"未知の文字 '{rows[row][col]}'(行 {row}, 列 {col})"
                        );
                }
            }
        }

        return new StageDefinition
        {
            TileSize = tileSize,
            Rows = rows,
            PlayerSpawn =
                playerSpawn ?? throw new StageMapException("P(プレイヤー初期位置)がありません"),
            MobSpawns = mobSpawns,
            BossSpawn =
                bossSpawn ?? throw new StageMapException("B(強敵の出現ポイント)がありません"),
            TurretSlot =
                turretSlot ?? throw new StageMapException("T(タレットの設置スロット)がありません"),
        };
    }
}
