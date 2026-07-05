using R3;
using VitalRouter;

namespace SuikaGame.Logic;

/// <summary>
/// ゲームコマンド(D-032)の受け口。発行されたコマンドを GameFlow の遷移へ配線する。
/// 終了だけはロジックでは完結しないため、ExitRequests として外(Godot 層)へ通知する。
/// </summary>
public sealed class GameCommandRouter : ICommandSubscriber, IDisposable
{
    public GameCommandRouter(GameFlow flow) => throw new NotImplementedException();

    /// <summary>コマンドの発行先。UI・外部コマンドはここへ Publish する。</summary>
    public Router Router => throw new NotImplementedException();

    /// <summary>終了要求の通知。Godot 層が購読してプロセスを終了する。</summary>
    public Observable<Unit> ExitRequests => throw new NotImplementedException();

    public void Receive<T>(T command, PublishContext context)
        where T : ICommand => throw new NotImplementedException();

    public void Dispose() => throw new NotImplementedException();
}
