using System;
using Godot;

namespace MessBreak;

/// <summary>
/// 論理座標↔描画座標の変換とエイム寄りカメラ。
/// ウィンドウ下端の UI バーを除いたゲーム画面を基準にする。
/// Draw / Input / Hud が共通で依存する下位レイヤー。
/// </summary>
public sealed class GameCamera
{
    /// <summary>UI バー(画面下部)の高さ。ゲーム画面はウィンドウからこの帯を除いた領域。</summary>
    public const float UiBarHeight = 96f;

    /// <summary>論理座標→描画座標の基本倍率(カメラズーム 1.0 のとき)。</summary>
    private const float Zoom = 3f;

    /// <summary>カメラをプレイヤーからカーソル側へ寄せる割合(非構え / 構え)。</summary>
    private const float LookAheadWeight = 0.12f;
    private const float LookAheadWeightAds = 0.3f;

    /// <summary>カメラ位置の追従率(毎フレーム)。小さいほどゆっくり=揺れにくい。</summary>
    private const float CameraLerp = 0.06f;

    /// <summary>構え(右クリック)中のズーム倍率。覗き込みの 2D 翻訳。</summary>
    private const float AdsZoom = 1.15f;

    private Vector2 _viewportSize;
    private System.Numerics.Vector2 _camPos;
    private float _camZoom = 1f;

    /// <summary>
    /// ゲーム画面領域(ウィンドウから下部 UI バーを除いた部分)。HUD はこの外に置く。
    /// ストレッチは使わず、UI は実ピクセルで一定・ゲームはウィンドウが広いほど視界が広がる。
    /// </summary>
    public Rect2 GameRect => new(0f, 0f, _viewportSize.X, _viewportSize.Y - UiBarHeight);

    /// <summary>ゲーム画面中心の描画座標。</summary>
    public Vector2 ScreenCenter => GameRect.GetCenter();

    /// <summary>論理座標でのカメラ中心。</summary>
    public System.Numerics.Vector2 Position
    {
        get => _camPos;
        set => _camPos = value;
    }

    /// <summary>構えズームを含む実効ズーム(1 = 非構え)。</summary>
    public float ZoomFactor
    {
        get => _camZoom;
        set => _camZoom = value;
    }

    /// <summary>論理座標→描画座標の実効倍率(基本倍率 × カメラズーム)。</summary>
    public float ScaleFactor => Zoom * _camZoom;

    /// <summary>ウィンドウサイズを反映する。毎フレーム呼ぶ。</summary>
    public void SetViewportSize(Vector2 viewportSize) => _viewportSize = viewportSize;

    /// <summary>
    /// カメラをプレイヤーとカーソルの間へ置き、エイムした方へ視界が伸びるようにする。
    /// 構え中は寄りを強め、わずかにズームイン(TPS の覗き込みの 2D 翻訳)。
    /// </summary>
    public void Update(
        System.Numerics.Vector2 playerPos,
        System.Numerics.Vector2 aimPoint,
        bool ads,
        float stageWidth,
        float stageHeight
    )
    {
        var weight = ads ? LookAheadWeightAds : LookAheadWeight;
        var desired = playerPos + (aimPoint - playerPos) * weight;

        var zoomTarget = ads ? AdsZoom : 1f;
        _camZoom += (zoomTarget - _camZoom) * 0.1f;

        // 画面が部屋の外を映さない範囲にクランプ。
        // 視界が部屋より広い(大きなウィンドウ)軸は部屋の中心に固定する
        var halfW = ScreenCenter.X / ScaleFactor;
        var halfH = ScreenCenter.Y / ScaleFactor;
        desired = new System.Numerics.Vector2(
            ClampAxis(desired.X, halfW, stageWidth),
            ClampAxis(desired.Y, halfH, stageHeight)
        );
        _camPos += (desired - _camPos) * CameraLerp;
    }

    /// <summary>描画座標を論理座標へ写す(マウス位置の変換用)。</summary>
    public System.Numerics.Vector2 ToLogic(Vector2 screen) =>
        new(
            (screen.X - ScreenCenter.X) / ScaleFactor + _camPos.X,
            (screen.Y - ScreenCenter.Y) / ScaleFactor + _camPos.Y
        );

    /// <summary>論理座標(左上原点)を描画座標へ写す。</summary>
    public Vector2 ToScreen(System.Numerics.Vector2 position) =>
        new(
            (position.X - _camPos.X) * ScaleFactor + ScreenCenter.X,
            (position.Y - _camPos.Y) * ScaleFactor + ScreenCenter.Y
        );

    /// <summary>カメラ中心の1軸クランプ。視界の半分が部屋の半分を超えるなら中心固定。</summary>
    private static float ClampAxis(float value, float halfView, float roomSize) =>
        halfView * 2f >= roomSize
            ? roomSize / 2f
            : Math.Clamp(value, halfView, roomSize - halfView);
}
