using Lidgren.Network;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared._VDS.Preferences;

/// <summary>
/// Message containing color data that is sent to ServerChatOOCColorManager.cs.
/// </summary>
#nullable disable
public sealed class MsgUpdateOOCColor : NetMessage
{
    public override MsgGroups MsgGroup => MsgGroups.Command;

    public string OOCColor = null!;

    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
    {
        OOCColor = buffer.ReadString();
    }

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
    {
        buffer.Write(OOCColor);

    }
}
