using System;
using System.IO;

namespace DigitalWorldOnline.Commons.Writers
{
    /// <summary>
    /// Classe responsável por escrever dados para pacotes de rede.
    /// Implementa a serialização e o cálculo de checksum do pacote.
    /// </summary>
    public class PacketWriter : PacketWriterBase
    {
        /// <summary>
        /// O buffer serializado, gerado apenas quando o método Serialize é chamado pela primeira vez.
        /// </summary>
        private byte[]? _serializedBuffer;

        /// <summary>
        /// Obtém o buffer serializado, se já tiver sido gerado. Uso interno.
        /// </summary>
        public byte[]? Buffer => _serializedBuffer;

        /// <summary>
        /// Inicializa uma nova instância da classe PacketWriter com um cabeçalho de 2 bytes.
        /// </summary>
        public PacketWriter() : base()
        {
            Packet = new MemoryStream();
            // Reserva espaço para o cabeçalho de comprimento (2 bytes)
            Packet.Write(new byte[] { 0, 0 }, 0, 2);
        }

        /// <summary>
        /// Inicializa uma nova instância da classe PacketWriter com um tipo específico de pacote.
        /// </summary>
        /// <param name="packetType">O tipo de pacote a ser serializado</param>
        public PacketWriter(int packetType) : this()
        {
            Type(packetType);
        }

        /// <summary>
        /// Serializa o pacote, calculando seu comprimento total e checksum.
        /// Após a primeira chamada, o buffer é armazenado em cache para chamadas subsequentes.
        /// </summary>
        /// <returns>Um array de bytes representando o pacote completo</returns>
        public byte[] Serialize()
        {
            // Se o pacote já foi serializado, retorna o buffer em cache
            if (_serializedBuffer != null)
                return _serializedBuffer;

            try
            {
                // Adiciona espaço para o checksum (2 bytes) ao final do pacote
                WriteShort(0);

                // Converte o fluxo de memória para um array de bytes
                byte[] buffer = Packet.ToArray();

                // Calcula o comprimento total do pacote
                short length = (short)buffer.Length;

                // Converte o comprimento para bytes e copia para o início do buffer
                byte[] lengthBytes = BitConverter.GetBytes(length);
                Array.Copy(lengthBytes, 0, buffer, 0, 2);

                // Calcula o checksum e copia para o final do buffer
                byte[] checksumBytes = BitConverter.GetBytes((short)(length ^ CheckSumValidation));
                Array.Copy(checksumBytes, 0, buffer, length - 2, 2);

                // Armazena o buffer serializado em cache
                _serializedBuffer = buffer;

                return buffer;
            }
            finally
            {
                // Fecha o fluxo de memória
                Packet.Close();
            }
        }

        /// <summary>
        /// Limpa o buffer serializado, permitindo modificações adicionais no pacote.
        /// </summary>
        public void ResetBuffer()
        {
            _serializedBuffer = null;
        }

        /// <summary>
        /// Obtém o buffer de bytes serializado em formato de string legível.
        /// </summary>
        /// <returns>Representação string do pacote serializado</returns>
        public override string ToString()
        {
            return $"Pacote: {BitConverter.ToString(Serialize()).Replace("-", " ")}";
        }

        /// <summary>
        /// Sobrescrita do método Dispose para garantir limpeza adequada de recursos.
        /// </summary>
        public new void Dispose()
        {
            _serializedBuffer = null;
            base.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}