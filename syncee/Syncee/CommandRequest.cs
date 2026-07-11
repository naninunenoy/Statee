using MemoryPack;

namespace Syncee;

/// <summary>
/// クライアントからサーバへの着手要求(D-050)。確定前の生コマンドで、
/// クライアント識別はトランスポートの接続そのもの(サーバが接続ごとに割り当てる)が担うため
/// ここには含まない。サーバは <see cref="AuthorityLog.TrySubmit"/> の引数に写像する。
/// </summary>
[MemoryPackable]
public sealed partial record CommandRequest(
    string Command,
    IReadOnlyDictionary<string, string>? Args
);
