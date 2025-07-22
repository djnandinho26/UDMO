using AutoMapper;
using DigitalWorldOnline.Application.Separar.Commands.Create;
using DigitalWorldOnline.Application.Separar.Commands.Delete;
using DigitalWorldOnline.Application.Separar.Queries;
using DigitalWorldOnline.Commons.Entities;
using DigitalWorldOnline.Commons.Enums;
using DigitalWorldOnline.Commons.Enums.Character;
using DigitalWorldOnline.Commons.Enums.PacketProcessor;
using DigitalWorldOnline.Commons.Extensions;
using DigitalWorldOnline.Commons.Interfaces;
using DigitalWorldOnline.Commons.Models.Account;
using DigitalWorldOnline.Commons.Models.Asset;
using DigitalWorldOnline.Commons.Models.Character;
using DigitalWorldOnline.Commons.Models.Digimon;
using DigitalWorldOnline.Commons.Packets.CharacterServer;
using DigitalWorldOnline.Commons.Utils;
using DigitalWorldOnline.Commons.Writers;
using MediatR;
using Microsoft.Extensions.Configuration;
using Serilog;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DigitalWorldOnline.Character.PacketProcessors
{
    internal class RecvRemoveTamer : ICharacterPacketProcessor
    {
        public CharacterServerPacketEnum Type => CharacterServerPacketEnum.DeleteCharacter;

        private readonly ISender _sender;
        private readonly IMapper _mapper;
        private readonly ILogger _logger;
        private readonly IConfiguration _configuration;

        public RecvRemoveTamer(
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
                _logger.Debug("Processando requisição de delete de domador de {ClientAddress}", client.ClientAddress);


                // Cria um leitor de pacotes para processar os dados recebidos  
                using var stream = new MemoryStream(packetData);
                using var reader = new BinaryReader(stream);

                _logger.Debug("Lendo parâmetros do pacote...");

                var characterId = reader.ReadInt64();
                var validation = reader.ReadString();

                _logger.Debug($"Conta de pesquisa com ID {client.AccountId}...");
                var account = _mapper.Map<AccountModel>(await _sender.Send(new AccountByIdQuery(client.AccountId)));

                if (account.CharacterDeleteValidation(validation))
                {
                    _logger.Debug($"Excluindo o personagem ...");
                    var deletedCharacter = await _sender.Send(new DeleteCharacterCommand(client.AccountId, characterId));

                    client.Send(new CharacterDeletedPacket(deletedCharacter).Serialize());
                }
                else
                {
                    _logger.Debug($"A validação falha para excluir o caráter em conta {account.Username}.");

                    client.Send(new CharacterDeletedPacket(DeleteCharacterResultEnum.ValidationFail).Serialize());
                }

            }
            catch (EndOfStreamException ex)
            {
                _logger.Error(ex, "Dados insuficientes no pacote de delete de personagem de {ClientAddress}: {Message}",
                    client.ClientAddress, ex.Message);
                client.Send(new CharacterCreationErrorPacket("Dados do pacote incompletos."));
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Erro ao processar requisição de delete de personagem de {ClientAddress}: {Message}",
                    client.ClientAddress, ex.Message);
                client.Send(new CharacterCreationErrorPacket("Erro interno do servidor."));
            }
        }

    }
}