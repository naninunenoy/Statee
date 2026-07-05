using R3;
using VitalRouter;

namespace SuikaGame.Logic;

/// <summary>
/// ゲームコマンド(D-032)の受け口。発行されたコマンドを GameFlow の遷移へ配線する。
/// 終了だけはロジックでは完結しないため、ExitRequests として外(Godot 層)へ通知する。
/// </summary>
public sealed class GameCommandRouter : ICommandSubscriber, IDisposable
{
    private readonly GameFlow _flow;
    private readonly Subject<Unit> _exitRequests = new();

    public GameCommandRouter(GameFlow flow)
    {
        _flow = flow;
        Router.Subscribe(this);
    }

    /// <summary>コマンドの発行先。UI・外部コマンドはここへ Publish する。</summary>
    public Router Router { get; } = new();

    /// <summary>終了要求の通知。Godot 層が購読してプロセスを終了する。</summary>
    public Observable<Unit> ExitRequests => _exitRequests;

    public void Receive<T>(T command, PublishContext context)
        where T : ICommand
    {
        switch (command)
        {
            case StartGameCommand:
                _flow.StartGame();
                break;
            case ExitGameCommand:
                _exitRequests.OnNext(Unit.Default);
                break;
        }
    }

    public void Dispose()
    {
        Router.Dispose();
        _exitRequests.Dispose();
    }
}
