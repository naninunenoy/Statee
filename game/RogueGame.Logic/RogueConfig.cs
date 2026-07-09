namespace RogueGame.Logic;

/// <summary>ゲームバランスとダンジョン生成のパラメータ。</summary>
public static class RogueConfig
{
    /// <summary>マップの幅(マス数)。</summary>
    public const int MapWidth = 40;

    /// <summary>マップの高さ(マス数)。</summary>
    public const int MapHeight = 24;

    /// <summary>フロア数。最下層(この値)に 💎 がある。</summary>
    public const int FloorCount = 5;
}
