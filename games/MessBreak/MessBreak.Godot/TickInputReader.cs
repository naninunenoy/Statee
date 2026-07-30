using System;
using Godot;
using MessBreak.Logic;

namespace MessBreak;

/// <summary>
/// 人間プレイのキー／マウス入力と、tick コマンドの入力トークン解析。
/// どちらも TickInput へ写し、Logic へ渡す入口を一本化する。
/// Main / Statee が依存する入力変換レイヤー。
/// </summary>
public static class TickInputReader
{
    /// <summary>人間プレイの入力(押されているキーの集合)を TickInput へ写す。</summary>
    public static TickInput ReadHuman(
        GameCamera camera,
        Vector2 mouseScreen,
        System.Numerics.Vector2 playerPos
    )
    {
        var dir = System.Numerics.Vector2.Zero;
        if (Input.IsPhysicalKeyPressed(Key.Left) || Input.IsPhysicalKeyPressed(Key.A))
        {
            dir.X -= 1f;
        }
        if (Input.IsPhysicalKeyPressed(Key.Right) || Input.IsPhysicalKeyPressed(Key.D))
        {
            dir.X += 1f;
        }
        if (Input.IsPhysicalKeyPressed(Key.Up) || Input.IsPhysicalKeyPressed(Key.W))
        {
            dir.Y -= 1f;
        }
        if (Input.IsPhysicalKeyPressed(Key.Down) || Input.IsPhysicalKeyPressed(Key.S))
        {
            dir.Y += 1f;
        }
        // マウスは常にカーソル方向を送る(CS2D 方式)。向きは常にカーソルが決め、
        // 移動は向きに関与しない。構え(右クリック)はズームと精密射撃の担当で、
        // 向きには影響しない(docs/DESIGN.md「向きと射撃」)
        var fire =
            Input.IsMouseButtonPressed(MouseButton.Left)
            || Input.IsPhysicalKeyPressed(Key.Z)
            || Input.IsPhysicalKeyPressed(Key.J);
        var mouseLogic = camera.ToLogic(mouseScreen);
        var aim = mouseLogic - playerPos;
        return new TickInput(
            dir,
            aim,
            Fire: fire,
            Dodge: Input.IsPhysicalKeyPressed(Key.Space),
            Sprint: Input.IsPhysicalKeyPressed(Key.Shift),
            Skill: Input.IsPhysicalKeyPressed(Key.E),
            AimPoint: mouseLogic, // マウスにはレティクル位置が常にある
            SwitchTo: Input.IsPhysicalKeyPressed(Key.Key1) ? CharacterId.Attacker
                : Input.IsPhysicalKeyPressed(Key.Key2) ? CharacterId.Debuffer
                : null,
            Interact: Input.IsPhysicalKeyPressed(Key.F)
        );
    }

    /// <summary>tick コマンドの aimx / aimy 引数(未指定は 0)を読む。</summary>
    public static float ParseFloat(string? value) =>
        value is null ? 0f : float.Parse(value, System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>"right+fire" のような + 区切りトークンと aim を TickInput へ写す。</summary>
    public static TickInput Parse(
        string tokens,
        System.Numerics.Vector2 aim,
        System.Numerics.Vector2? aimPoint
    )
    {
        var dir = System.Numerics.Vector2.Zero;
        var fire = false;
        var dodge = false;
        var sprint = false;
        var skill = false;
        var interact = false;
        CharacterId? switchTo = null;
        foreach (var token in tokens.Split('+', StringSplitOptions.RemoveEmptyEntries))
        {
            switch (token.Trim().ToLowerInvariant())
            {
                case "-":
                    break; // 無入力
                case "left":
                    dir.X -= 1f;
                    break;
                case "right":
                    dir.X += 1f;
                    break;
                case "up":
                    dir.Y -= 1f;
                    break;
                case "down":
                    dir.Y += 1f;
                    break;
                case "fire":
                    fire = true;
                    break;
                case "dodge":
                    dodge = true;
                    break;
                case "sprint":
                    sprint = true;
                    break;
                case "skill":
                    skill = true;
                    break;
                case "char1":
                    switchTo = CharacterId.Attacker;
                    break;
                case "char2":
                    switchTo = CharacterId.Debuffer;
                    break;
                case "interact":
                    interact = true;
                    break;
                default:
                    throw new ArgumentException(
                        $"未知の入力トークン '{token}'(left/right/up/down/fire/dodge/sprint/skill/char1/char2/interact)"
                    );
            }
        }
        return new TickInput(dir, aim, fire, dodge, sprint, skill, aimPoint, switchTo, interact);
    }
}
