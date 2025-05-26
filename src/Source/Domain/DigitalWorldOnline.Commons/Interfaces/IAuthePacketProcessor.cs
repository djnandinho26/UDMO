using DigitalWorldOnline.Commons.Enums.PacketProcessor;

namespace DigitalWorldOnline.Commons.Interfaces
{
    public interface IAuthePacketProcessor : IPacketProcessor
    {
        public AuthenticationServerPacketEnum Type { get; }
    }
}