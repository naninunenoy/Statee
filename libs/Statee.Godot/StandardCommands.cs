using System;
using System.IO;
using Godot;
using Microsoft.Extensions.Logging;
using Statee.Core;
using ZLogger;

namespace Statee.Godot;

/// <summary>
/// どのゲームにも共通の標準コマンド(ping / key / screenshot / quit)を一括登録する。
/// ping は組み込みではないため、疎通確認の起点としてここで必ず登録する。
/// </summary>
public static class StandardCommands
{
    /// <summary>"ctrl+space" 形式のキー指定を分解する。修飾子は ctrl / shift / alt。</summary>
    private static (Key Key, bool Ctrl, bool Shift, bool Alt) ParseKey(string name)
    {
        var parts = name.Split('+', StringSplitOptions.TrimEntries);
        var key = Enum.Parse<Key>(parts[^1], ignoreCase: true);
        var ctrl = false;
        var shift = false;
        var alt = false;
        foreach (var modifier in parts[..^1])
        {
            switch (modifier.ToLowerInvariant())
            {
                case "ctrl":
                    ctrl = true;
                    break;
                case "shift":
                    shift = true;
                    break;
                case "alt":
                    alt = true;
                    break;
                default:
                    throw new InvalidOperationException($"未知の修飾キー: {modifier}");
            }
        }
        return (key, ctrl, shift, alt);
    }

    public static void Register(StateeHost host, Node node, ILogger logger)
    {
        // 接続先プロセスの同一性確認(system/identity)。古いバイナリ・別プロセスへの
        // 接続事故を検証の冒頭で検出できるよう、全ゲーム共通で公開する(D-075)
        host.RegisterStateProvider(new IdentityStateProvider(node.GetType().Assembly));
        host.RegisterCommand(
            "ping",
            args =>
            {
                var message = args.GetString("message") ?? "ping";
                logger.ZLogInformation($"ping を受信: {message}");
                return new { Pong = true, Message = message };
            }
        );
        // 実際の入力経路(PushInput)を通すため、入力配線ごと検証できる
        host.RegisterMainThreadCommand(
            "key",
            args =>
            {
                var name =
                    args.GetString("key")
                    ?? throw new InvalidOperationException(
                        "key を指定すること(例: space, ctrl+space)"
                    );
                var (key, ctrl, shift, alt) = ParseKey(name);
                var viewport = node.GetViewport();
                viewport.PushInput(
                    new InputEventKey
                    {
                        Keycode = key,
                        CtrlPressed = ctrl,
                        ShiftPressed = shift,
                        AltPressed = alt,
                        Pressed = true,
                    }
                );
                viewport.PushInput(
                    new InputEventKey
                    {
                        Keycode = key,
                        CtrlPressed = ctrl,
                        ShiftPressed = shift,
                        AltPressed = alt,
                        Pressed = false,
                    }
                );
                logger.ZLogInformation($"key {name}");
                return new { Key = name };
            }
        );
        host.RegisterMainThreadCommand(
            "screenshot",
            args =>
            {
                var path =
                    args.GetString("path")
                    ?? throw new InvalidOperationException("path を指定すること(絶対パス)");
                var image =
                    node.GetViewport().GetTexture()?.GetImage()
                    ?? throw new InvalidOperationException(
                        "描画が無いため撮影できない(headless では screenshot は使えない。D-034)"
                    );
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".");
                var error = image.SavePng(path);
                if (error != Error.Ok)
                {
                    throw new InvalidOperationException($"スクリーンショット保存失敗: {error}");
                }
                logger.ZLogInformation($"screenshot path={path}");
                return new { Path = Path.GetFullPath(path) };
            }
        );
        // 動作確認は「quit で exit 0」まで含めて検証する
        host.RegisterMainThreadCommand(
            "quit",
            _ =>
            {
                logger.ZLogInformation($"quit を受信。終了する");
                node.GetTree().Quit();
                return new { Quitting = true };
            }
        );
    }
}
