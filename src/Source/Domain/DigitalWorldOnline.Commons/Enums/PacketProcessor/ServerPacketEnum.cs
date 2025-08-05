namespace DigitalWorldOnline.Commons.Enums.PacketProcessor
{
    public enum ServerPacketEnum
    {
        Unknow,
        Connection = 65535, // -1 as ushort
        KeepConnection = 65533 // -3 as ushort
    }
}
