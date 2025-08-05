using DigitalWorldOnline.Commons.Enums.PacketProcessor;
using DigitalWorldOnline.Commons.Interfaces;
using DigitalWorldOnline.Commons.Readers;

namespace DigitalWorldOnline.Game
{
    /// <summary>
    /// Leitor de pacotes específico para o servidor de jogo.
    /// Responsável por interpretar os dados recebidos do cliente relacionados ao jogo.
    /// </summary>
    public class GamePacketReader : PacketReaderBase, IPacketReader
    {
        /// <summary>
        /// Obtém o tipo de pacote como um enum específico do servidor de jogo.
        /// </summary>
        public GameServerPacketEnum Enum => (GameServerPacketEnum)Type;

        /// <summary>
        /// Inicializa uma nova instância do leitor de pacotes para o servidor de jogo.
        /// </summary>
        /// <param name="buffer">Array de bytes contendo os dados do pacote.</param>
        /// <exception cref="ArgumentNullException">Lançada quando o buffer é nulo.</exception>
        /// <exception cref="ArgumentException">Lançada quando o buffer é muito pequeno para ser um pacote válido.</exception>
        /// <exception cref="Exception">Lançada quando o checksum do pacote é inválido.</exception>
        public GamePacketReader(byte[] buffer)
        {
            // Validação do buffer de entrada
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer), "O buffer do pacote não pode ser nulo.");
            
            if (buffer.Length < 6) // Tamanho mínimo para um pacote válido (2 bytes para Length + 2 bytes para Type + 2 bytes para Checksum)
                throw new ArgumentException("O buffer do pacote é muito pequeno para ser um pacote válido.", nameof(buffer));
            
            try
            {
                // Inicializa o stream com o buffer fornecido
                Packet = new(buffer);

                // Lê o comprimento do pacote (primeiros 2 bytes)
                Length = ReadShort();

                // Lê o tipo do pacote (próximos 2 bytes)
                Type = ReadUShort();

                // Navega até a posição do checksum (final do pacote menos 2 bytes)
                Packet.Seek(Length - 2, SeekOrigin.Begin);

                // Lê o valor do checksum
                int checksum = ReadShort();

                // Verifica se o checksum é válido
                if (checksum != (Length ^ CheckSumValidation))
                    throw new Exception($"Checksum do pacote inválido: esperado {Length ^ CheckSumValidation}, recebido {checksum}");

                // Reposiciona o cursor após os bytes de cabeçalho para leitura dos dados
                Packet.Seek(4, SeekOrigin.Begin);
            }
            catch (Exception ex) when (!(ex is ArgumentNullException || ex is ArgumentException))
            {
                // Captura e relança exceções com informações adicionais sobre o pacote
                throw new Exception($"Erro ao processar pacote de jogo: {ex.Message}", ex);
            }
        }
    }
}
