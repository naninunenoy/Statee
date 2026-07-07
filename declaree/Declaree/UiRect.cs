namespace Declaree;

/// <summary>
/// レイアウト確定後の画面上の矩形(グローバル座標)。IR には存在せず、
/// 描画済みの Control から実行時に採取される。クリック座標の導出や幾何述語に使う。
/// </summary>
public record UiRect(float X, float Y, float Width, float Height);
