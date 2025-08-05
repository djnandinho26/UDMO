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
    public class RecvChangeServer: ICharacterPacketProcessor
    {
        public CharacterServerPacketEnum Type => CharacterServerPacketEnum.ConnectGameServer;

        private readonly ISender _sender;
        private readonly IMapper _mapper;
        private readonly ILogger _logger;
        private readonly IConfiguration _configuration;
        private const string GameServerAddress = "GameServer:Address";
        private const string GameServerPort = "GameServer:Port";

        public RecvChangeServer(
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
                
                _logger.Debug("Enviando resposta para conectar ao servidor do jogo..");
                client.Send(new ConnectGameServerPacket().Serialize());

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