using DigitalWorldOnline.Commons.Models.Base;
using DigitalWorldOnline.Commons.Models.Character;
using DigitalWorldOnline.Commons.Utils;
using DigitalWorldOnline.Commons.Writers;
using System.Collections.Generic;
using static System.Reflection.Metadata.BlobBuilder;

namespace DigitalWorldOnline.Commons.Packets.GameServer
{
    public class EncyclopediaReceiveRewardItemPacket : PacketWriter
    {
        private const int PacketNumber = 3235;

        public EncyclopediaReceiveRewardItemPacket(ItemModel item, int digimonId)
        {
            Type(PacketNumber);

            WriteUInt((uint)item.ItemId);
            WriteUShort((ushort)item.Amount);
        }
    }
}