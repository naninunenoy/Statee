using Godot;
using Statee.Core;

namespace Statee.Godot;

public static partial class StandardCommands
{
    static partial void RegisterIdentity(StateeHost host, Node node) =>
        host.RegisterStateProvider(new IdentityStateProvider(node.GetType().Assembly));
}
