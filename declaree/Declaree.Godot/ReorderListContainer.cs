using System;
using Godot;

namespace Declaree.Godot;

/// <summary>
/// <see cref="ReorderList"/> の実体。VBoxContainer の直接の子が IR の子と1対1対応する
/// (UiSnapshot の並行走査を壊さないため、ドラッグハンドル等の中間ノードを挟まない)。
/// 子の上で押下 → 縦に閾値以上動かす → 離す、で並び替えイベントを発行する。
/// 閾値未満の単純クリックはドラッグにならず、子(Button 等)へそのまま届く。
/// </summary>
public partial class ReorderListContainer : VBoxContainer
{
    private const float DragThreshold = 8f;

    private int _pressedIndex = -1;
    private float _pressedY;
    private bool _dragging;

    /// <summary>ドラッグ並び替えの確定(fromIndex, toIndex)。</summary>
    public event Action<int, int>? Reordered;

    public override void _Input(InputEvent @event)
    {
        // 子(Button 等)がマウスイベントを消費しても並び替えを成立させるため、
        // _GuiInput ではなく _Input で観測する。押下〜解放の観測のみで、イベントは消費しない
        switch (@event)
        {
            case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } press:
                _pressedIndex = IndexAt(press.Position);
                _pressedY = press.Position.Y;
                _dragging = false;
                break;
            case InputEventMouseMotion motion when _pressedIndex >= 0:
                if (Math.Abs(motion.Position.Y - _pressedY) >= DragThreshold)
                {
                    _dragging = true;
                }
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false } release:
                if (
                    _dragging
                    && _pressedIndex >= 0
                    && IndexAt(release.Position) is var to
                    && to >= 0
                )
                {
                    if (to != _pressedIndex)
                    {
                        Reordered?.Invoke(_pressedIndex, to);
                    }
                }
                _pressedIndex = -1;
                _dragging = false;
                break;
        }
    }

    /// <summary>グローバル座標 position が載っている直接の子のインデックス。外れていれば -1。</summary>
    private int IndexAt(Vector2 position)
    {
        for (var i = 0; i < GetChildCount(); i++)
        {
            if (GetChild(i) is Control child && child.GetGlobalRect().HasPoint(position))
            {
                return i;
            }
        }
        return -1;
    }
}
