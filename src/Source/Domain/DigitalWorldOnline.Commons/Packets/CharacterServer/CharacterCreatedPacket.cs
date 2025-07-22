using DigitalWorldOnline.Commons.Enums.ClientEnums;
using DigitalWorldOnline.Commons.Models.Character;
using DigitalWorldOnline.Commons.Writers;

namespace DigitalWorldOnline.Commons.Packets.CharacterServer
{
    public class CharacterCreatedPacket : PacketWriter
    {
        private const int PacketNumber = 1306;

        public CharacterCreatedPacket(CharacterModel character, short handshake)
        {
            Type(PacketNumber);

            WriteInt64(handshake);
            WriteInt64(character.Id);
            WriteInt64(handshake);
            WriteShort(character.Location.MapId);
            WriteInt(character.Model.GetHashCode());
            WriteByte(1);
            WriteString(character.Name);

            for (int i = 0; i < GeneralSizeEnum.Equipment.GetHashCode(); i++)
                WriteBytes(new byte[59]);

            WriteInt(character.Partner.Model.GetHashCode());
            WriteByte(1);
            WriteString(character.Partner.Name);
            WriteInt(10000);
        }
    }
}
