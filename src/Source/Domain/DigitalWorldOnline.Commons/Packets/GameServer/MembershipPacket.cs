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
            int miliseconds = utcSeconds * 776;     // 775 = 6 dias (776 = 96 dias)
            //Console.WriteLine($"Miliseconds calculation: {(membershipExpirationDate - DateTime.UtcNow).TotalSeconds * 200}");   // 240
            //Console.WriteLine($"miliseconds: {miliseconds} | duration: {utcSeconds}");

            if (miliseconds <= 0)
                miliseconds = 0;
            
            Type(PacketNumber);
            WriteByte(Convert.ToByte(utcSeconds > 0));
            WriteInt(miliseconds);
        }

        public MembershipPacket()
        {
            Type(PacketNumber);
            WriteByte(0);
            WriteInt(0);
        }
    }
}