using DigitalWorldOnline.Commons.Enums.PacketProcessor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DigitalWorldOnline.Commons.Interfaces
{
    public interface ICharacterPacketProcessor : IPacketProcessor
    {
        public CharacterServerPacketEnum Type { get; }
    }
}
