using AutoMapper;
using DigitalWorldOnline.Application.Separar.Commands.Update;
using DigitalWorldOnline.Application.Separar.Queries;
using DigitalWorldOnline.Character.Configuration;
using DigitalWorldOnline.Commons.Entities;
using DigitalWorldOnline.Commons.Enums.PacketProcessor;
using DigitalWorldOnline.Commons.Interfaces;
using DigitalWorldOnline.Commons.Models.Account;
using DigitalWorldOnline.Commons.Models.Character;
using DigitalWorldOnline.Commons.Packet;
using DigitalWorldOnline.Commons.Packets.CharacterServer;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Serilog;

namespace DigitalWorldOnline.Character.PacketProcessors
{
    public class RecvAccessCode : ICharacterPacketProcessor
    {
        public CharacterServerPacketEnum Type => CharacterServerPacketEnum.RequestCharacters;

        private readonly ISender _sender;
        private readonly IMapper _mapper;
        private readonly ILogger _logger;
        private readonly IConfiguration _configuration;

        public RecvAccessCode(
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
        /// Processa a requisição de personagens do cliente.  
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
                _logger.Debug("Processando requisição de personagens de {ClientAddress}", client.ClientAddress);

                // Cria um leitor de pacotes para processar os dados recebidos  
                using var stream = new MemoryStream(packetData);
                using var packet = new BinaryReader(stream);

                var checkCode = packet.ReadInt32();
                var accountId = packet.ReadInt32();
                var accessCode = packet.ReadInt32();

                _logger.Debug("AccountId: {AccountId}, AccessCode: {AccessCode}", accountId, accessCode);

                _logger.Information($"Obtendo a lista de personagens da conta {accountId}...");
                var characters = _mapper.Map<List<CharacterModel>>(await _sender.Send(new CharactersByAccountIdQuery(accountId)));

                characters.ForEach(character =>
                {
                    if (character.Partner.CurrentType != character.Partner.BaseType)
                    {
                        _logger.Debug($"Atualizando o tipo atual do parceiro...");
                        character.Partner.UpdateCurrentType(character.Partner.BaseType);
                        _sender.Send(new UpdatePartnerCurrentTypeCommand(character.Partner));
                    }
                });

                client.Send(new CharacterListPacket(characters));

                client.SetAccountId(accountId);


                _logger.Debug("Lista de personagens enviada com sucesso para {ClientAddress}", client.ClientAddress);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Erro ao processar requisição de personagens de {ClientAddress}: {Message}",
                    client.ClientAddress, ex.Message);
                client.Disconnect();
            }
        }
    }
} 