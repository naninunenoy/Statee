namespace Statee.Core;

/// <summary>State スナップショットの提供者。パス(例: "system")単位で StateeHost に登録する。</summary>
public interface IStateProvider
{
    string Path { get; }

    object CaptureState();
}
