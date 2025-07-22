using DigitalWorldOnline.Commons.Writers;

namespace DigitalWorldOnline.Commons.Packets.AuthenticationServer
{
    public class ConnectCharacterServerPacket : PacketWriter
    {
        private const int PacketNumber = 901;

        /// <summary>
        /// Selected server from server list.
        /// </summary>
        public ConnectCharacterServerPacket(long accountId, string ipAddress, string port)
        {
            Type(PacketNumber);
            WriteUInt((uint)accountId);
            WriteInt((int)accountId); 
            WriteString(ipAddress);
            WriteUInt(uint.Parse(port));
        }
    }
}
