using DigitalWorldOnline.Commons.Writers;

namespace DigitalWorldOnline.Commons.Packets.GameServer
{
    public class ConnectionPacket : PacketWriter
    {
        private const ushort PacketNumber = 65534; // -2 as ushort

        /// <summary>
        /// Used on client requesting connection with the server.
        /// </summary>
        /// <param name="handshake">The server-client handshake</param>
        /// <param name="handshakeTimestamp">Timestamp when the handshake has been accepted</param>
        public ConnectionPacket(short handshake, uint handshakeTimestamp)
        {
            Type(PacketNumber);
            WriteShort(handshake);
            WriteUInt(handshakeTimestamp);
        }
    }
}
