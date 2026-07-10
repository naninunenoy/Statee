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
    public static void Register(StateeHost host, Node node, ILogger logger)
    {
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
                    ?? throw new InvalidOperationException("key を指定すること(例: space)");
                var key = Enum.Parse<Key>(name, ignoreCase: true);
                var viewport = node.GetViewport();
                viewport.PushInput(new InputEventKey { Keycode = key, Pressed = true });
                viewport.PushInput(new InputEventKey { Keycode = key, Pressed = false });
                logger.ZLogInformation($"key {key}");
                return new { Key = key.ToString() };
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
