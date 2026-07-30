using System;
using Godot;
using MessBreak.Logic;

namespace MessBreak;

/// <summary>
/// 下部 UI バー・ミッションガイド・ポーズメニュー。
/// 見た目の State(ui/hud)はノードの実表示・実レイアウトから写す。
/// Main が依存する UI レイヤー(ゲームルールは持たない)。
/// </summary>
public sealed class HudView
{
    private const float HpBarWidth = 200f;

    private Label _missionLabel = null!;
    private Panel _uiBar = null!;
    private Label _hpLabel = null!;
    private ColorRect _hpBack = null!;
    private ColorRect _hpFill = null!;
    private Label _char1Label = null!;
    private Label _char2Label = null!;
    private Label _switchLabel = null!;
    private CanvasLayer _pauseLayer = null!;
    private Button _resumeButton = null!;

    public bool PauseMenuVisible => _pauseLayer?.Visible ?? false;

    /// <summary>
    /// 下部 UI バーを組み立てる。ゲーム画面(GameRect)の外に置き、盤面へ被せない。
    /// 内容は厳選: ミッションガイド / キャラ2枠(スキル CD)/ 切替 CD。
    /// 詳細な検証値(tick・shot 数等)は画面でなく State(game/messbreak)で見る。
    /// </summary>
    public void Build(Node parent)
    {
        var layer = new CanvasLayer();
        parent.AddChild(layer);

        // バーはウィンドウ下端に実ピクセルでアンカー(リサイズしても高さ・文字サイズは一定)
        var bar = new Panel();
        _uiBar = bar;
        bar.SetAnchorsPreset(Control.LayoutPreset.BottomWide);
        bar.OffsetTop = -GameCamera.UiBarHeight;
        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.07f, 0.06f, 0.09f),
            BorderColor = new Color(0.35f, 0.3f, 0.45f),
            BorderWidthTop = 2,
        };
        bar.AddThemeStyleboxOverride("panel", style);
        layer.AddChild(bar);

        // プレイヤー HP(数値+バー)。バーは背景の上に残量ぶんの塗りを重ねる
        _hpLabel = MakeLabel(bar, new Vector2(24f, 14f), 18, new Color(0.6f, 1f, 0.65f));
        _hpBack = new ColorRect
        {
            Position = new Vector2(24f, 52f),
            Size = new Vector2(HpBarWidth, 14f),
            Color = new Color(0.2f, 0.22f, 0.2f),
        };
        bar.AddChild(_hpBack);
        _hpFill = new ColorRect
        {
            Size = new Vector2(HpBarWidth, 14f),
            Color = new Color(0.35f, 0.85f, 0.45f),
        };
        _hpBack.AddChild(_hpFill);

        // ミッションガイドはゲーム領域の左上に重ねる(視線移動を減らす)。縁取りで盤面から浮かせる
        var overlay = new CanvasLayer();
        parent.AddChild(overlay);
        _missionLabel = new Label { Position = new Vector2(16f, 12f) };
        _missionLabel.AddThemeFontSizeOverride("font_size", 18);
        _missionLabel.AddThemeColorOverride("font_color", new Color(1f, 0.95f, 0.8f));
        _missionLabel.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.85f));
        _missionLabel.AddThemeConstantOverride("outline_size", 6);
        overlay.AddChild(_missionLabel);

        // キャラ枠と切替 CD はバーの水平中央に追従させる
        _char1Label = MakeLabel(bar, new Vector2(0f, 14f), 18, Colors.White);
        _char2Label = MakeLabel(bar, new Vector2(0f, 50f), 18, Colors.White);
        _switchLabel = MakeLabel(bar, new Vector2(0f, 32f), 14, new Color(1f, 1f, 1f, 0.6f));
        AnchorToBarCenter(_char1Label, -120f);
        AnchorToBarCenter(_char2Label, -120f);
        AnchorToBarCenter(_switchLabel, 110f);
    }

    /// <summary>ポーズメニュー(Esc)。全画面の暗幕+中央の縦ボタン列。</summary>
    public void BuildPauseMenu(Node parent, Action onResume, Action onRestart, Action onQuit)
    {
        _pauseLayer = new CanvasLayer { Layer = 10, Visible = false };
        parent.AddChild(_pauseLayer);

        var dim = new ColorRect { Color = new Color(0f, 0f, 0f, 0.6f) };
        dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _pauseLayer.AddChild(dim);

        // VBox へ直接 Center アンカーを設定するとサイズ確定前の左上角が中心に置かれるため、
        // 全画面の CenterContainer に包ませて常に画面中心へレイアウトさせる
        var center = new CenterContainer();
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _pauseLayer.AddChild(center);

        var menu = new VBoxContainer();
        menu.AddThemeConstantOverride("separation", 12);
        center.AddChild(menu);

        var title = new Label { Text = "ポーズ", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 28);
        menu.AddChild(title);

        _resumeButton = MakeMenuButton(menu, "再開", onResume);
        MakeMenuButton(menu, "はじめから", onRestart);
        var abortButton = MakeMenuButton(menu, "中断", () => { });
        // 戻り先(タイトル画面)ができるまで押せない。選択肢の枠だけ先に用意しておく
        abortButton.Disabled = true;
        abortButton.TooltipText = "タイトル画面の実装後に有効化";
        MakeMenuButton(menu, "ゲーム終了", onQuit);
    }

    /// <summary>ポーズ表示の開閉。戻り値は新しいポーズ状態。</summary>
    public bool SetPaused(bool paused)
    {
        _pauseLayer.Visible = paused;
        Input.MouseMode = paused ? Input.MouseModeEnum.Visible : Input.MouseModeEnum.Hidden;
        if (paused)
        {
            _resumeButton.GrabFocus();
        }
        return paused;
    }

    /// <summary>UI バーの表示をロジックの現在値から作り直し、ui/hud State へ写す。</summary>
    public void Update(
        BattleLogic logic,
        GameCamera camera,
        BattleView battleView,
        bool paused,
        HudState hudState
    )
    {
        _missionLabel.Text =
            logic.MissionCleared ? "ミッション達成!"
            : !logic.ZoneCaptured ? "雑魚を倒してエリアを制圧しよう"
            : !logic.TurretPlaced ? "スロット(緑枠)の近くで F: タレット設置"
            : !logic.BossAppeared ? "出現ポイント(赤菱形)の近くで F: 強敵を呼ぶ"
            : "強敵を倒せ!(デバフ→大技のコンボが有効)";

        var active = logic.ActiveCharacter;
        _char1Label.Text =
            $"{(active == CharacterId.Attacker ? "▶" : "  ")} 1 アタッカー  E {CooldownText(logic, logic.SkillCooldownOf(CharacterId.Attacker))}";
        _char2Label.Text =
            $"{(active == CharacterId.Debuffer ? "▶" : "  ")} 2 デバッファー  E {CooldownText(logic, logic.SkillCooldownOf(CharacterId.Debuffer))}";
        _char1Label.AddThemeColorOverride(
            "font_color",
            active == CharacterId.Attacker ? Colors.White : new Color(1f, 1f, 1f, 0.45f)
        );
        _char2Label.AddThemeColorOverride(
            "font_color",
            active == CharacterId.Debuffer
                ? new Color(0.55f, 0.9f, 1f)
                : new Color(1f, 1f, 1f, 0.45f)
        );
        _switchLabel.Text =
            logic.SwitchCooldown > 0 ? $"切替 {CooldownText(logic, logic.SwitchCooldown)}" : "";

        var maxHp = logic.Config.PlayerMaxHp;
        _hpLabel.Text = $"HP {logic.PlayerHp}/{maxHp}";
        _hpFill.Size = _hpFill.Size with
        {
            X = HpBarWidth * Math.Clamp(logic.PlayerHp / (float)maxHp, 0f, 1f),
        };

        var sprite = battleView.PlayerSpriteAppearance();
        hudState.Update(
            new HudState.Snapshot(
                MissionText: _missionLabel.Text,
                MissionRect: RectText(_missionLabel.GetGlobalRect()),
                HpText: _hpLabel.Text,
                HpBarRatio: _hpFill.Size.X / HpBarWidth,
                HpBarRect: RectText(_hpBack.GetGlobalRect()),
                Char1Text: _char1Label.Text,
                Char1Rect: RectText(_char1Label.GetGlobalRect()),
                Char2Text: _char2Label.Text,
                Char2Rect: RectText(_char2Label.GetGlobalRect()),
                SwitchText: _switchLabel.Text,
                UiBarRect: RectText(_uiBar.GetGlobalRect()),
                GameRect: RectText(camera.GameRect),
                PauseMenuVisible: PauseMenuVisible,
                PlayerSpriteCharacter: sprite.Character.ToString(),
                PlayerSpriteDirection: BattleSprites.DirectionName(sprite.Row),
                PlayerSpriteColumn: sprite.Column,
                PlayerSpriteMirrored: sprite.Mirror
            )
        );
    }

    private static string CooldownText(BattleLogic logic, int ticks) =>
        ticks == 0 ? "READY" : $"{ticks / (float)logic.Config.TicksPerSecond:0.0}s";

    private static void AnchorToBarCenter(Label label, float offsetX)
    {
        var top = label.Position.Y;
        label.AnchorLeft = 0.5f;
        label.AnchorRight = 0.5f;
        label.OffsetLeft = offsetX;
        label.OffsetTop = top;
    }

    private static Label MakeLabel(Control parent, Vector2 pos, int fontSize, Color color)
    {
        var label = new Label { Position = pos };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_color", color);
        parent.AddChild(label);
        return label;
    }

    private static Button MakeMenuButton(VBoxContainer menu, string text, Action onPressed)
    {
        var button = new Button { Text = text, CustomMinimumSize = new Vector2(220f, 40f) };
        button.Pressed += onPressed;
        menu.AddChild(button);
        return button;
    }

    private static string RectText(Rect2 rect) =>
        $"{rect.Position.X:0},{rect.Position.Y:0},{rect.Size.X:0},{rect.Size.Y:0}";
}
