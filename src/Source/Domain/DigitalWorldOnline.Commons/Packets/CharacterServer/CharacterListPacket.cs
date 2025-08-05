using DigitalWorldOnline.Commons.Enums.ClientEnums;
using DigitalWorldOnline.Commons.Models.Character;
using DigitalWorldOnline.Commons.Writers;

namespace DigitalWorldOnline.Commons.Packets.CharacterServer
{
    public class CharacterListPacket : PacketWriter
    {
        private const int PacketNumber = 1301;

        // CORREÇÃO: Constantes para valores repetidos
        private const short ReservedFieldValue = 0;

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

                // Equipment slots
                WriteEquipmentSlots(character);

                // Partner information
                WritePartnerInfo(character);

                // Final fields
                WriteReservedFields(character);
            }
        }

        /// <summary>
        /// Escreve os slots de equipamento com valores padrão
        /// </summary>
        private void WriteEquipmentSlots(CharacterModel character)
        {
            for (int i = 0; i < GeneralSizeEnum.Equipment.GetHashCode(); i++)
            {
                WriteBytes(character.Equipment.Items[i].ToArray(true));
            }
        }

        /// <summary>
        /// Escreve as informações do partner do personagem
        /// </summary>
        /// <param name="character">Modelo do personagem</param>
        private void WritePartnerInfo(CharacterModel character)
        {
            WriteInt(character.Partner.BaseType);
            WriteByte(character.Partner.Level);
            WriteString(character.Partner.Name);
            WriteInt(character.Partner.Size);
        }

        /// <summary>
        /// CORREÇÃO: Escreve os campos reservados/finais do pacote garantindo que todos os valores sejam escritos
        /// </summary>
        /// <param name="character">Modelo do personagem</param>
        private void WriteReservedFields(CharacterModel character)
        {
            WriteShort((short)character.EffectType);

            // SealLeaderId (1 byte) - com proteção null
            byte sealLeaderId = (byte)(character.SealList?.SealLeaderId ?? 0);
            WriteByte(sealLeaderId);

            // Segundo campo reservado (2 bytes)
            WriteShort((short)character.ServerTranf);
        }
    }
}