using AutoMapper;
using DigitalWorldOnline.Account.Models.Configuration;
using DigitalWorldOnline.Application.Separar.Commands.Update;
using DigitalWorldOnline.Application.Separar.Queries;
using DigitalWorldOnline.Commons.DTOs.Config;
using DigitalWorldOnline.Commons.Entities;
using DigitalWorldOnline.Commons.Enums.PacketProcessor;
using DigitalWorldOnline.Commons.Interfaces;
using DigitalWorldOnline.Commons.Models.Servers;
using DigitalWorldOnline.Commons.Packets.AuthenticationServer;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace DigitalWorldOnline.Account.PacketProcessors
{
    internal class RecvSelectCluster : IAuthePacketProcessor
    {
        public AuthenticationServerPacketEnum Type => AuthenticationServerPacketEnum.ConnectCharacterServer;

        private readonly ISender _sender;
        private readonly IMapper _mapper;
        private readonly ILogger _logger;
        private readonly IConfiguration _configuration;
        private readonly AuthenticationServerConfigurationModel _authenticationServerConfiguration;

        // Constante para a chave de configuração do endereço do servidor de personagens
        private const string CharacterServerAddress = "CharacterServer:Address";

        public RecvSelectCluster(
            ISender sender,
            IMapper mapper,
            ILogger logger,
            IConfiguration configuration,
            IOptions<AuthenticationServerConfigurationModel> authenticationServerConfiguration)
        {
            _sender = sender ?? throw new ArgumentNullException(nameof(sender));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _authenticationServerConfiguration = authenticationServerConfiguration?.Value
                ?? throw new ArgumentNullException(nameof(authenticationServerConfiguration));
        }

        private void DebugLog(string message)
        {
            _logger?.Debug($"{message}");
        }

        /// <summary>
        /// Processa a requisição de lista de servidores do cliente.
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
                DebugLog($"Leitura de parâmetros de pacotes ...");

                using var stream = new MemoryStream(packetData);
                using var reader = new BinaryReader(stream);

                // Lê o ID do servidor selecionado pelo cliente
                var serverId = reader.ReadInt32();

                // Atualiza o último servidor utilizado pelo jogador no banco de dados
                await _sender.Send(new UpdateLastPlayedServerCommand(client.AccountId, serverId));

                DebugLog($"Obtendo a lista de servidores ...");
                // Consulta a lista de servidores disponíveis para o nível de acesso do cliente
                var servers =
                    _mapper.Map<IEnumerable<ServerObject>>(
                        await _sender.Send(new ServersQuery(client.AccessLevel)));

                // Localiza o servidor específico selecionado pelo cliente
                var targetServer = servers.FirstOrDefault(x => x.Id == serverId);

                // Verifica se o servidor foi encontrado
                if (targetServer == null)
                {
                    throw new InvalidOperationException($"Servidor com ID {serverId} não encontrado");
                }

                DebugLog($"Enviando informações do servidor selecionado...");
                // Envia ao cliente os dados necessários para conexão com o servidor de personagens
                client.Send(new ConnectCharacterServerPacket(client.AccountId,
                    _configuration[CharacterServerAddress], targetServer.Port.ToString()));
            }
            catch (Exception ex)
            {
                // Registra o erro no log
                _logger.Error(ex, "Erro ao processar requisição de seleção de servidor: {Mensagem}", ex.Message);

                // Envia uma lista vazia para evitar travamento do cliente
                try
                {
                    client.Send(new ServerListPacket(new List<ServerObject>()).Serialize());
                }
                catch
                {
                    // Ignora erros ao tentar enviar resposta de fallback
                }
            }
        }
    }
}