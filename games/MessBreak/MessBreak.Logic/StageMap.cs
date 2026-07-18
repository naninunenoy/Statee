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
    public static StageDefinition Parse(string text, float tileSize = 40f) => default!;
}
