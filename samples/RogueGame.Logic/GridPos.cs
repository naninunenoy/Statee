namespace RogueGame.Logic;

/// <summary>グリッド上の座標(左上原点、X 右方向・Y 下方向)。</summary>
public readonly record struct GridPos(int X, int Y);
