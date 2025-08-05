using AutoMapper;
using DigitalWorldOnline.Application.Separar.Queries;
using DigitalWorldOnline.Commons.Entities;
using DigitalWorldOnline.Commons.Enums.PacketProcessor;
using DigitalWorldOnline.Commons.Extensions;
using DigitalWorldOnline.Commons.Interfaces;
using DigitalWorldOnline.Commons.Models.Account;
using DigitalWorldOnline.Commons.Packets.CharacterServer;
using MediatR;
using Microsoft.Extensions.Configuration;
using Serilog;
using System.Text;
namespace DigitalWorldOnline.Character.PacketProcessors
{
    internal class RecvCheckDoubleName : ICharacterPacketProcessor
    {
        public CharacterServerPacketEnum Type => CharacterServerPacketEnum.CheckNameDuplicity;

        private readonly ISender _sender;
        private readonly IMapper _mapper;
        private readonly ILogger _logger;
        private readonly IConfiguration _configuration;

        public RecvCheckDoubleName(
            ISender sender,
            IMapper mapper,
            ILogger logger,
            IConfiguration configuration)
        {
            _sender = sender ?? throw new ArgumentNullException(nameof(sender));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        /// <summary>  
        /// Processa a requisição de verificação de duplicidade de nome do cliente.  
        /// </summary>  
        /// <param name="client">Cliente do jogo que enviou o pacote</param>  
        /// <param name="packetData">Dados do pacote recebido</param>  
        /// <returns>Task representando a operação assíncrona</returns>  
        public async Task Process(GameClient client, byte[] packetData)
        {
            // Validação dos parâmetros de entrada  
            ArgumentNullException.ThrowIfNull(client, nameof(client));
            ArgumentNullException.ThrowIfNull(packetData, nameof(packetData));

            try
            {
                _logger.Debug("Processando requisição de Nome Duplicidade de {ClientAddress}", client.ClientAddress);

                // Verifica se há dados suficientes para processar (pelo menos 2 bytes para o tamanho)
                if (packetData.Length < 2)
                {
                    _logger.Warning("Pacote de verificação de nome muito pequeno: {Length} bytes", packetData.Length);
                    client.Disconnect();
                    return;
                }

                // Cria um leitor de pacotes para processar os dados recebidos  
                using var stream = new MemoryStream(packetData);
                using var reader = new BinaryReader(stream);

                _logger.Debug("Obtendo parâmetros ...");

                // Lê o tamanho da string (2 bytes = short)
                short nameLength = reader.ReadInt16();

                // Valida o tamanho da string
                if (nameLength < 0 || nameLength > 32) // Limite razoável para nome de personagem
                {
                    _logger.Warning("Tamanho de nome inválido: {NameLength} de {ClientAddress}",
                        nameLength, client.ClientAddress);
                    client.Disconnect();
                    return;
                }

                // Verifica se há bytes suficientes para ler a string completa
                if (stream.Position + nameLength > packetData.Length)
                {
                    _logger.Warning("Dados insuficientes para ler nome completo. Esperado: {Expected}, Disponível: {Available}",
                        nameLength, packetData.Length - stream.Position);
                    client.Disconnect();
                    return;
                }

                // Lê a string com o tamanho especificado
                byte[] nameBytes = reader.ReadBytes(nameLength);
                string tamerName = Encoding.UTF8.GetString(nameBytes).Trim('\0'); // Remove null terminators se houver

                _logger.Debug($"Conta: {client.AccountId} - Nome: '{tamerName}' (Tamanho: {nameLength})");

                // Validação adicional do nome
                if (string.IsNullOrWhiteSpace(tamerName))
                {
                    _logger.Warning("Nome de personagem vazio ou inválido de {ClientAddress}", client.ClientAddress);
                    client.Send(new AvailableNamePacket(false).Serialize());
                    return;
                }

                _logger.Debug("Buscando conta ...");
                var account = _mapper.Map<AccountModel>(await _sender.Send(new AccountByIdQuery(client.AccountId)));

                if (account == null)
                {
                    _logger.Warning("Conta não encontrada para ID {AccountId}", client.AccountId);
                    client.Disconnect();
                    return;
                }

                // Aplica prefixo de moderador se necessário
                tamerName = tamerName.ModeratorPrefix(account.AccessLevel);
                _logger.Debug($"Nome processado: '{tamerName}'");

                _logger.Debug("Verificando a duplicidade do nome do domador ...");
                var existingCharacter = await _sender.Send(new CharacterByNameQuery(tamerName));
                bool availableName = existingCharacter == null;

                _logger.Debug($"Nome disponível: {availableName}");

                _logger.Debug("Enviando resposta...");
                client.Send(new AvailableNamePacket(availableName).Serialize());
            }
            catch (EndOfStreamException ex)
            {
                _logger.Error(ex, "Dados insuficientes no pacote de verificação de nome de {ClientAddress}: {Message}",
                    client.ClientAddress, ex.Message);
                client.Disconnect();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Erro ao processar requisição de Nome Duplicidade de {ClientAddress}: {Message}",
                    client.ClientAddress, ex.Message);
                client.Disconnect();
            }
        }
    }
}