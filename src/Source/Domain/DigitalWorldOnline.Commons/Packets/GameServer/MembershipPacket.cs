using DigitalWorldOnline.Commons.Writers;

namespace DigitalWorldOnline.Commons.Packets.GameServer
{
    public class MembershipPacket : PacketWriter
    {
        private const int PacketNumber = 3414;

        /// <summary>
        /// Load the account remaining membership time.
        /// </summary>
        public MembershipPacket(DateTime membershipExpirationDate, int utcSeconds)
        {
            Type(PacketNumber);
            
            // Should be sent in milliseconds not seconds
            int remainingSeconds = (int)(membershipExpirationDate - DateTime.UtcNow).TotalMilliseconds;
            WriteByte(Convert.ToByte(remainingSeconds > 0));
            WriteInt(remainingSeconds);
        }

        public MembershipPacket()
        {
            Type(PacketNumber);
            WriteByte(0);
            WriteInt(0);
        }
    }
}