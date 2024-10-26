using DigitalWorldOnline.Commons.Models.Base;
using DigitalWorldOnline.Commons.Models.Character;
using DigitalWorldOnline.Commons.Utils;
using DigitalWorldOnline.Commons.Writers;
using System.Collections.Generic;
using static System.Reflection.Metadata.BlobBuilder;

namespace DigitalWorldOnline.Commons.Packets.GameServer
{
    public class EncyclopediaDeckBuffUsePacket : PacketWriter
    {
        private const int PacketNumber = 3235;

        public EncyclopediaDeckBuffUsePacket(int HP, short AS)
        {
            Type(PacketNumber);

            WriteInt(HP);
            WriteUShort((ushort)AS);
        }
    }
}