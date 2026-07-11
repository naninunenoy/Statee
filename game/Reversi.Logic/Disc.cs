namespace Reversi.Logic;

/// <summary>盤面のマスの状態(石)。</summary>
public enum Disc
{
    None,
    Black,
    White,
}

public static class DiscExtensions
{
    /// <summary>相手の石を返す。None に対しては None。</summary>
    public static Disc Opponent(this Disc disc) =>
        disc switch
        {
            Disc.Black => Disc.White,
            Disc.White => Disc.Black,
            _ => Disc.None,
        };
}
