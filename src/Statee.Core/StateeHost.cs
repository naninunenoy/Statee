using System.Collections.Concurrent;
using Cysharp.AI;

namespace Statee.Core;

/// <summary>
/// State/Command/Log を束ね、リクエストを処理するフレームワークの中心。
/// 組み込みコマンド: state(path)/ logs(tail)。トランスポートには依存しない。
/// コマンド・プロバイダの登録は待ち受け開始前に済ませる想定。
/// </summary>
public sealed class StateeHost
{
    private readonly ConcurrentDictionary<string, CommandHandler> _commands = new();
    private readonly ConcurrentDictionary<string, IStateProvider> _providers = new();

    public StateeHost(LogBuffer? logBuffer = null)
    {
        Logs = logBuffer ?? new LogBuffer(1024);
        RegisterCommand("state", HandleStateCommand);
        RegisterCommand("logs", HandleLogsCommand);
    }

    public LogBuffer Logs { get; }

    /// <summary>
    /// メインスレッドで実行するコマンド(RegisterMainThreadCommand)用のディスパッチャ。
    /// ゲーム側が生成して設定し、メインループから Pump を呼ぶ。
    /// </summary>
    public MainThreadDispatcher? MainThreadDispatcher { get; set; }

    public void RegisterCommand(string name, CommandHandler handler) => _commands[name] = handler;

    /// <summary>
    /// メインスレッドで実行するコマンドを登録する(Godot API 等を触るコマンド用)。
    /// 実行には MainThreadDispatcher の設定が必要。
    /// </summary>
    public void RegisterMainThreadCommand(string name, CommandHandler handler) =>
        RegisterCommand(
            name,
            args =>
            {
                var dispatcher =
                    MainThreadDispatcher
                    ?? throw new InvalidOperationException(
                        $"コマンド '{name}' の実行には MainThreadDispatcher の設定が必要"
                    );
                return dispatcher.Run(() => handler(args));
            }
        );

    public void RegisterStateProvider(IStateProvider provider) =>
        _providers[provider.Path] = provider;

    public StateeResponse HandleRequest(StateeRequest request)
    {
        if (!_commands.TryGetValue(request.Command, out var handler))
        {
            return StateeResponse.Fail(request.Id, $"未知のコマンド: {request.Command}");
        }

        try
        {
            var result = handler(new CommandArgs(request.Args));
            var payload = result is null ? string.Empty : ToonEncoder.Encode(result);
            return StateeResponse.Ok(request.Id, payload);
        }
        catch (Exception e)
        {
            return StateeResponse.Fail(request.Id, e.Message);
        }
    }

    private object HandleStateCommand(CommandArgs args)
    {
        var path = args.GetString("path") ?? throw new ArgumentException("path 引数が必要");
        if (!_providers.TryGetValue(path, out var provider))
        {
            throw new KeyNotFoundException($"未知の State パス: {path}");
        }

        return provider.CaptureState();
    }

    private object HandleLogsCommand(CommandArgs args)
    {
        var tail = args.GetInt("tail", 50);
        return Logs.Tail(tail);
    }
}
