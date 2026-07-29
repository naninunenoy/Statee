namespace Voxcee;

/// <summary>RGBA 1 ボクセルの色。</summary>
public readonly record struct Rgba(byte R, byte G, byte B, byte A)
{
    public bool IsSolid => A > 0;

    public float Rf => R / 255f;
    public float Gf => G / 255f;
    public float Bf => B / 255f;
    public float Af => A / 255f;
}
