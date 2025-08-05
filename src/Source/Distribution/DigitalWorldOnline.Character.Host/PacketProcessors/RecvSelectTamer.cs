using AutoMapper;
using DigitalWorldOnline.Application.Separar.Commands.Update;
using DigitalWorldOnline.Application.Separar.Queries;
using DigitalWorldOnline.Commons.Entities;
using DigitalWorldOnline.Commons.Enums.PacketProcessor;
using DigitalWorldOnline.Commons.Interfaces;
using DigitalWorldOnline.Commons.Models.Character;
using DigitalWorldOnline.Commons.Packets.CharacterServer;
using MediatR;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace DigitalWorldOnline.Character.PacketProcessors
{
    internal class RecvSelectTamer: ICharacterPacketProcessor
    {
        public CharacterServerPacketEnum Type => CharacterServerPacketEnum.GetCharacterPosition;

        private readonly ISender _sender;
        private readonly IMapper _mapper;
        private readonly ILogger _logger;
        private readonly IConfiguration _configuration;
        private const string GameServerAddress = "GameServer:Address";
        private const string GameServerPort = "GameServer:Port";

        public RecvSelectTamer(
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
        /// Processa a requisição de criação de personagem do cliente.  
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
                _logger.Debug("Processando requisição de seleção de domador de {ClientAddress}", client.ClientAddress);
                
                // Cria um leitor de pacotes para processar os dados recebidos  
                using var stream = new MemoryStream(packetData);
                using var packet = new BinaryReader(stream);
                
                var position = packet.ReadInt64();

                _logger.Debug($"Searching character...");
                var character = _mapper.Map<CharacterModel>(await _sender.Send(new CharacterByAccountIdAndPositionQuery(client.AccountId, position)));

                while (character == null)
                {
                    await Task.Delay(1500);
                    _logger.Debug($"Searching character again...");
                    character = _mapper.Map<CharacterModel>(await _sender.Send(new CharacterByAccountIdAndPositionQuery(client.AccountId, position)));
                }

                _logger.Debug($"Updating access information for account {client.AccountId}.");
                await _sender.Send(new UpdateLastPlayedCharacterCommand(client.AccountId, character.Id));

                _logger.Debug($"Updating character's channel...");
                await _sender.Send(new UpdateCharacterChannelCommand(character.Id));

                _logger.Debug($"Updating account welcome flag...");
                await _sender.Send(new UpdateAccountWelcomeFlagCommand(character.AccountId));

                _logger.Debug($"Updating character send once packet...");
                await _sender.Send(new UpdateCharacterInitialPacketSentOnceSentCommand(character.Id, false));

                _logger.Debug($"Sending selected server info...");
                client.Send(new ConnectGameServerInfoPacket(
                    _configuration[GameServerAddress],
                    _configuration[GameServerPort],
                    character.Location.MapId).Serialize());
                

            }
            catch (EndOfStreamException ex)
            {
                _logger.Error(ex, "Dados insuficientes no pacote de criação de personagem de {ClientAddress}: {Message}",
                    client.ClientAddress, ex.Message);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Erro ao processar requisição de criação de personagem de {ClientAddress}: {Message}",
                    client.ClientAddress, ex.Message);
            }
        }

    }
}