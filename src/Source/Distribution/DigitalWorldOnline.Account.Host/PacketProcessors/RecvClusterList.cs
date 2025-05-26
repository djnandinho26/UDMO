using AutoMapper;
using DigitalWorldOnline.Account.Models.Configuration;
using DigitalWorldOnline.Application.Separar.Queries;
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
using System.Threading.Tasks;

namespace DigitalWorldOnline.Account.PacketProcessors
{
    internal class RecvClusterList : IAuthePacketProcessor
    {
        public AuthenticationServerPacketEnum Type => AuthenticationServerPacketEnum.LoadServerList;

        private readonly ISender _sender;
        private readonly IMapper _mapper;
        private readonly ILogger _logger;
        private readonly IConfiguration _configuration;
        private readonly AuthenticationServerConfigurationModel _authenticationServerConfiguration;

        public RecvClusterList(
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
                DebugLog($"Obtendo a lista de servidores para o nível de acesso: {client.AccessLevel}...");

                // Obtém a lista de servidores do banco de dados
                var serverDtos = await _sender.Send(new ServersQuery(client.AccessLevel));

                if (serverDtos == null)
                {
                    _logger.Warning("A consulta ServersQuery retornou nulo. Verifique a implementação do handler.");
                    client.Send(new ServerListPacket(new List<ServerObject>()).Serialize());
                    return;
                }

                // Mapeia para o modelo ServerObject
                var serverObjects = new List<ServerObject>();
                foreach (var dto in serverDtos)
                {
                    // Mapeamento manual caso o AutoMapper esteja falhando
                    try
                    {
                        var serverObj = _mapper.Map<ServerObject>(dto);
                        if (serverObj != null)
                        {
                            serverObjects.Add(serverObj);
                        }
                        else
                        {
                            _logger.Warning($"Falha ao mapear ServerDTO para ServerObject: {dto.Id}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, $"Erro ao mapear ServerDTO para ServerObject: {dto.Id}");
                    }
                }

                DebugLog($"Obtidos {serverObjects.Count} servidores. Atualizando contagens de personagens...");

                // Atualiza as contagens de personagens para cada servidor
                foreach (var server in serverObjects)
                {
                    try
                    {
                        int characterCount = await _sender.Send(new CharactersInServerQuery(client.AccountId, server.Id));
                        server.UpdateCharacterCount(Convert.ToByte(characterCount));
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, $"Erro ao obter contagem de personagens para servidor {server.Id}");
                        // Continua mesmo se houver erro, apenas não atualiza a contagem
                        server.UpdateCharacterCount(0);
                    }
                }

                DebugLog($"Enviando lista com {serverObjects.Count} servidores para o cliente...");
                client.Send(new ServerListPacket(serverObjects).Serialize());
            }
            catch (Exception ex)
            {
                // Registra o erro no log
                _logger.Error(ex, "Erro ao processar requisição de lista de servidores: {Mensagem}", ex.Message);

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