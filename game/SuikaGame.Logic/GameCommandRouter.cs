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
    private readonly Subject<Unit> _restartRequests = new();

    public GameCommandRouter(GameFlow flow)
    {
        _flow = flow;
        Router.Subscribe(this);
    }

    /// <summary>コマンドの発行先。UI・外部コマンドはここへ Publish する。</summary>
    public Router Router { get; } = new();

    /// <summary>終了要求の通知。Godot 層が購読してプロセスを終了する。</summary>
    public Observable<Unit> ExitRequests => _exitRequests;

    /// <summary>やり直し要求の通知(遷移成立時のみ)。Godot 層が購読して盤面・スコアをリセットする。</summary>
    public Observable<Unit> RestartRequests => _restartRequests;

    public void Receive<T>(T command, PublishContext context)
        where T : ICommand
    {
        switch (command)
        {
            case StartGameCommand:
                _flow.StartGame();
                break;
            case PauseGameCommand:
                _flow.PauseGame();
                break;
            case ResumeGameCommand:
                _flow.ResumeGame();
                break;
            case RestartGameCommand:
                if (_flow.RestartGame())
                {
                    _restartRequests.OnNext(Unit.Default);
                }
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
        _restartRequests.Dispose();
    }
}
