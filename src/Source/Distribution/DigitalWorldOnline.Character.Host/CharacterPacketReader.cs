﻿using DigitalWorldOnline.Commons.Entities;
using DigitalWorldOnline.Commons.Enums.PacketProcessor;
using DigitalWorldOnline.Commons.Packet;
using DigitalWorldOnline.Commons.Readers;
using Serilog;

namespace DigitalWorldOnline.Character
{
    /// <summary>
    /// Leitor de pacotes específico para o servidor de personagens.
    /// Responsável por interpretar os dados recebidos do cliente.
    /// </summary>
    public class CharacterPacketReader : PacketReaderBase
    {
        /// <summary>
        /// Obtém o tipo de pacote como um enum específico do servidor de personagens.
        /// </summary>
        public CharacterServerPacketEnum Enum => (CharacterServerPacketEnum)Type;

        private static readonly ILogger _logger = Log.ForContext<CharacterPacketReader>();

        public bool IsValid { get; private set; } = true;

        /// <summary>
        /// Inicializa uma nova instância do leitor de pacotes para o servidor de personagens.
        /// </summary>
        /// <param name="buffer">Array de bytes contendo os dados do pacote.</param>
        /// <exception cref="ArgumentNullException">Lançada quando o buffer é nulo.</exception>
        /// <exception cref="ArgumentException">Lançada quando o buffer é muito pequeno para ser um pacote válido.</exception>
        /// <exception cref="Exception">Lançada quando o checksum do pacote é inválido.</exception>
        public CharacterPacketReader(byte[] buffer, GameClient client = null)
        {
            if (buffer == null || buffer.Length < 10) // Verifica se o buffer tem pelo menos o tamanho mínimo necessário
                throw new ArgumentException("Buffer de pacote inválido ou muito pequeno", nameof(buffer));

            Packet = new(buffer);

            try
            {
                // Lê o tamanho e o tipo do pacote
                Length = ReadInt();
                Type = ReadShort();

                // Verifica se o tamanho declarado é válido
                if (Length == 0)
                {
                    IsValid = false;
                    _logger?.Warning("Pacote com tamanho zero detectado. Tipo: {Type}, Buffer: {Buffer}",
                        Type, BitConverter.ToString(buffer));

                    // Se não for um pacote de conexão, considera inválido
                    if (Enum != CharacterServerPacketEnum.Connection &&
                        Enum != CharacterServerPacketEnum.KeepConnection)
                    {
                        if (client != null)
                        {
                            _logger?.Warning("Desconectando cliente {ClientAddress} devido a pacote com tamanho zero",
                                client.ClientAddress);
                            client.Disconnect();
                        }
                        throw new ArgumentException("Pacote com tamanho zero");
                    }
                }
                else if (Length > buffer.Length || Length < 6)
                {
                    IsValid = false;
                    _logger?.Warning("Pacote com tamanho inválido: declarado {DeclaredLength}, real {ActualLength}",
                        Length, buffer.Length);

                    // Ajusta o tamanho para evitar erros de leitura
                    Length = (short)buffer.Length;

                    // Se for um pacote de conexão, não lançamos exceção
                    if (Enum != CharacterServerPacketEnum.Connection)
                    {
                        if (client != null)
                        {
                            _logger?.Warning("Desconectando cliente {ClientAddress} devido a pacote inválido",
                                client.ClientAddress);
                            client.Disconnect();
                        }
                        throw new Exception("Pacote com tamanho inválido");
                    }
                }

                // Verifica o checksum apenas se o pacote não for de conexão ou o checksum for necessário
                // E também verifica se o tamanho é maior que 0
                if (Length > 1 && Enum != CharacterServerPacketEnum.Connection &&
                    Enum != CharacterServerPacketEnum.KeepConnection)
                {
                    try
                    {
                        SysCons.LogPacketRecv($"{Type} \r\n{Dump.HexDump(buffer, Length)}");
                        _logger?.Error("Buffer recebido: {Buffer}", BitConverter.ToString(buffer));
                        ValidateChecksum();
                    }
                    catch (Exception ex)
                    {
                        IsValid = false;
                        _logger?.Warning(ex, "Erro de validação de checksum para pacote {PacketType}, Tamanho: {Length}",
                            Enum, Length);
                        _logger?.Error("Buffer recebido: {Buffer}", BitConverter.ToString(buffer));

                        // Se não for pacote de conexão, desconecta o cliente
                        if (client != null && Length > 0)
                        {
                            _logger?.Warning("Desconectando cliente {ClientAddress} devido a checksum inválido",
                                client.ClientAddress);
                            client.Disconnect();
                        }
                        throw;
                    }
                }
            }
            catch (Exception ex)
            {
                IsValid = false;
                _logger?.Error(ex, "Erro ao processar pacote: {Message}. Buffer: {Buffer}",
                    ex.Message, BitConverter.ToString(buffer));
                throw;
            }
            finally
            {
                // Posiciona o cursor depois do cabeçalho para leitura de dados
                // mesmo se houver erro, para permitir pelo menos a leitura do tipo de pacote
                try
                {
                    Packet.Seek(4, SeekOrigin.Begin);
                }
                catch
                {
                    // Ignore erros ao reposicionar o cursor
                }
            }
        }

        /// <summary>
        /// Valida o checksum do pacote para garantir sua integridade
        /// </summary>
        /// <exception cref="Exception">Lançada quando o checksum do pacote é inválido</exception>
        private void ValidateChecksum()
        {
            // Verificar se o pacote tem tamanho suficiente para conter um checksum
            if (Packet.Length < Length || Length < 8)
            {
                throw new Exception($"Pacote muito pequeno para conter checksum válido: {Length} bytes");
            }

            // Move para a posição do checksum (fim do pacote menos 4 bytes)
            Packet.Seek(Length - 4, SeekOrigin.Begin);
            // Lê o checksum e valida
            int checksum = ReadInt();

            // Se o checksum for 0, aceita o pacote sem validação
            if (checksum == 0)
            {
                _logger?.Debug("Checksum com valor 0 recebido. Ignorando validação de checksum.");
                return;
            }

            int expectedChecksum = Length ^ CheckSumValidation;

            if (checksum != expectedChecksum)
            {
                throw new Exception($"Checksum inválido: recebido {checksum}, esperado {expectedChecksum}");
            }
        }
    }
}
