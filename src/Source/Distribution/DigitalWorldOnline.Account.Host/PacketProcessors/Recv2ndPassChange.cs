using AutoMapper;
using DigitalWorldOnline.Account.Models.Configuration;
using DigitalWorldOnline.Application.Separar.Commands.Create;
using DigitalWorldOnline.Application.Separar.Queries;
using DigitalWorldOnline.Commons.Entities;
using DigitalWorldOnline.Commons.Enums.ClientEnums;
using DigitalWorldOnline.Commons.Enums.PacketProcessor;
using DigitalWorldOnline.Commons.Interfaces;
using DigitalWorldOnline.Commons.Packets.AuthenticationServer;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Serilog;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace DigitalWorldOnline.Account.PacketProcessors
{
    internal class Recv2ndPassChange : IAuthePacketProcessor
    {
        public AuthenticationServerPacketEnum Type => AuthenticationServerPacketEnum.SecondaryPasswordChange;

        private readonly ISender _sender;
        private readonly IMapper _mapper;
        private readonly ILogger _logger;
        private readonly IConfiguration _configuration;
        private readonly AuthenticationServerConfigurationModel _authenticationServerConfiguration;

        public Recv2ndPassChange(
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
            _logger?.Debug(message);
        }

        private static string ExtractSecondaryPassword(AuthenticationPacketReader packet)
        {
            const int size = 32;
            string data = Encoding.ASCII.GetString(packet.ReadBytes(size)).Trim();
            packet.ReadByte();
            return data;
        }

        public async Task Process(GameClient client, byte[] packetData)
        {
            ArgumentNullException.ThrowIfNull(client, nameof(client));
            ArgumentNullException.ThrowIfNull(packetData, nameof(packetData));

            try
            {
                var packet = new AuthenticationPacketReader(packetData);

                DebugLog("Processando solicitação de alteração de senha secundária");
                var currentSecurityCode = ExtractSecondaryPassword(packet);
                var newSecurityCode = ExtractSecondaryPassword(packet);

                DebugLog($"Buscando conta com ID {client.AccountId}");
                var account = await _sender.Send(new AccountByIdQuery(client.AccountId));

                if (account == null)
                {
                    throw new KeyNotFoundException($"Conta não encontrada para o ID {client.AccountId}");
                }

                if (account.SecondaryPassword == currentSecurityCode)
                {
                    await AlterarSenhaSecundaria(client.AccountId, newSecurityCode);
                    EnviarResultado(client, SecondaryPasswordChangeEnum.Changed);
                }
                else
                {
                    DebugLog("Senha secundária atual incorreta");
                    EnviarResultado(client, SecondaryPasswordChangeEnum.IncorretCurrentPassword);
                }
            }
            catch (KeyNotFoundException ex)
            {
                _logger.Warning(ex, "Conta não encontrada: {Mensagem}", ex.Message);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Erro ao processar alteração de senha secundária: {Mensagem}", ex.Message);
            }
        }

        private async Task AlterarSenhaSecundaria(long accountId, string newPassword)
        {
            DebugLog($"Salvando nova senha secundária para conta {accountId}");
            await _sender.Send(new CreateOrUpdateSecondaryPasswordCommand(accountId, newPassword));
        }

        private void EnviarResultado(GameClient client, SecondaryPasswordChangeEnum resultado)
        {
            DebugLog($"Enviando resposta de alteração de senha secundária: {resultado}");
            client.Send(new SecondaryPasswordChangeResultPacket(resultado));
        }
    }
}