using System;
using Godot;
using Statee.Core;

namespace Statee.Godot;

/// <summary>キーバインド表(D-039)の入力処理と State 公開。</summary>
public static class KeyBindingTable
{
    /// <summary>
    /// _UnhandledInput から呼ぶ。押下キーイベントを表に照らして最初に合致した
    /// 有効なバインドを発行する。処理したら true。
    /// </summary>
    public static bool TryHandle(KeyBinding[] bindings, InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true, Echo: false } keyEvent)
        {
            return false;
        }
        foreach (var binding in bindings)
        {
            if (binding.Key == keyEvent.Keycode && (binding.IsActive?.Invoke() ?? true))
            {
                binding.Publish();
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// バインド表を game/input State として公開するプロバイダを作る。
    /// 表は起動後不変の前提で、一度だけスナップショットを構築する。
    /// </summary>
    public static IStateProvider CreateInputStateProvider(KeyBinding[] bindings)
    {
        // ActiveIn(場面)の概念が無いゲームでは列ごと出さない
        object keys = Array.Exists(bindings, binding => binding.ActiveIn is not null)
            ? Array.ConvertAll(
                bindings,
                binding =>
                    (object)
                        new
                        {
                            Key = binding.Key.ToString(),
                            ActiveIn = binding.ActiveIn ?? "",
                            Publishes = binding.Publishes,
                            Explain = binding.Explain,
                        }
            )
            : Array.ConvertAll(
                bindings,
                binding =>
                    (object)
                        new
                        {
                            Key = binding.Key.ToString(),
                            Publishes = binding.Publishes,
                            Explain = binding.Explain,
                        }
            );
        var snapshot = new { Keys = keys };
        return new SnapshotStateProvider("game/input", () => snapshot);
    }
}
