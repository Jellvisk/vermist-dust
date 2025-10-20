using Content.Shared._VDS.Preferences;
using Robust.Shared.Network;

namespace Content.Client._VDS.Chat.Managers;

/// <summary>
/// Client implementation of the chat OOC color manager that sends color updates to the server.
/// </summary>
public sealed class ClientOOCColorManager : IClientOOCColorManager
{
    [Dependency] private readonly IClientNetManager _netManager = default!;
    public void Initialize()
    {
        IoCManager.InjectDependencies(this);
        _netManager.RegisterNetMessage<MsgUpdateOOCColor>();
    }
    public void HandleUpdateOOCColorMessage(Color color)
    {
        var msg = new MsgUpdateOOCColor()
        {
            OOCColor = color.ToHex(),
        };
        _netManager.ClientSendMessage(msg);
    }
}
