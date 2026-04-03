using ProtoBuf;

namespace quickpick;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class QuickPickRequest
{
    public int X;
    public int Y;
    public int Z;
}