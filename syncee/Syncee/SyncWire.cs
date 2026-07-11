using MemoryPack;

namespace Syncee;

/// <summary>
/// ワイヤシリアライズ(MemoryPack)の薄いラッパー(D-050)。
/// AuthorityLog/ReplicaLog 自体はシリアライズを知らないため、
/// トランスポートへの送受信を行う呼び出し側(Reversi.Server / Reversi.Godot 等)がこれを使う。
/// </summary>
public static class SyncWire
{
    public static byte[] Serialize(CommandRequest request) =>
        MemoryPackSerializer.Serialize(request);

    public static byte[] Serialize(CommandEnvelope envelope) =>
        MemoryPackSerializer.Serialize(envelope);

    public static CommandRequest DeserializeRequest(byte[] data) =>
        MemoryPackSerializer.Deserialize<CommandRequest>(data)
        ?? throw new InvalidOperationException("CommandRequest のデシリアライズに失敗した");

    public static CommandEnvelope DeserializeEnvelope(byte[] data) =>
        MemoryPackSerializer.Deserialize<CommandEnvelope>(data)
        ?? throw new InvalidOperationException("CommandEnvelope のデシリアライズに失敗した");
}
