using DigitalWorldOnline.Commons.Enums.ClientEnums;
using DigitalWorldOnline.Commons.Models.Character;
using DigitalWorldOnline.Commons.Writers;

namespace DigitalWorldOnline.Commons.Packets.CharacterServer
{
    public class CharacterListPacket : PacketWriter
    {
        private const int PacketNumber = 1301;

        public CharacterListPacket(IEnumerable<CharacterModel> characters)
        {
            Type(PacketNumber);

            // Filtra e converte para lista uma única vez para evitar múltiplas enumerações
            var validCharacters = characters?.Where(character => character?.Partner != null).ToList() ?? new List<CharacterModel>();

            WriteInt(validCharacters.Count);

            // Usa for loop para melhor performance e controle de exceções
            for (int index = 0; index < validCharacters.Count; index++)
            {
                var character = validCharacters[index];

                WriteInt64(character.Id);
                WriteShort(character.Location.MapId);
                WriteInt(character.Model.GetHashCode());
                WriteByte(character.Level);
                WriteString(character.Name);

                for (int i = 0; i < GeneralSizeEnum.Equipment.GetHashCode(); i++)
                {
                    //WriteBytes(character.Equipment.Items[i].ToArray(true));
                    WriteBytes(new byte[59]);
                }

                WriteInt(character.Partner.BaseType);
                WriteByte(character.Partner.Level);
                WriteString(character.Partner.Name);
                WriteShort(character.Partner.Size);

                //TODO: Ver o que esses 2 mudam
                WriteShort(0); //??
                WriteShort(character.SealList.SealLeaderId);
                WriteShort(0); //??
            }
        }
    }
}