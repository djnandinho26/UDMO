using AutoMapper;
using DigitalWorldOnline.Account.Models.Configuration;
using DigitalWorldOnline.Application.Separar.Commands.Create;
using DigitalWorldOnline.Application.Separar.Queries;
using DigitalWorldOnline.Commons.Entities;
using DigitalWorldOnline.Commons.Enums.Account;
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
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace DigitalWorldOnline.Account.PacketProcessors
{
    internal class RecvCheck2ndPass : IAuthePacketProcessor
    {
        public AuthenticationServerPacketEnum Type => AuthenticationServerPacketEnum.SecondaryPasswordCheck;

        private readonly ISender _sender;
        private readonly IMapper _mapper;
        private readonly ILogger _logger;
        private readonly IConfiguration _configuration;
        private readonly AuthenticationServerConfigurationModel _authenticationServerConfiguration;


        public RecvCheck2ndPass(
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

        private static string ExtractSecondaryPassword(AuthenticationPacketReader packet)
        {
            const int size = 32;
            string data = Encoding.ASCII.GetString(packet.ReadBytes(size)).Trim();
            // O próximo byte é lido mas não utilizado, pode ser removido se não for necessário
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

                DebugLog("Iniciando processamento do pacote de verificação de senha secundária.");

                var needToCheck = packet.ReadShort() == SecondaryPasswordCheckEnum.Check.GetHashCode();

                DebugLog($"Buscando conta com ID {client.AccountId}.");
                var account = await _sender.Send(new AccountByIdQuery(client.AccountId));

                if (account == null)
                    throw new KeyNotFoundException($"Conta não encontrada para o ID {client.AccountId}.");

                if (needToCheck)
                {
                    DebugLog("Lendo senha secundária do pacote.");
                    var securityCode = ExtractSecondaryPassword(packet);

                    if (account.SecondaryPassword == securityCode)
                    {
                        await RegistrarTentativaLogin(account.Username, client.ClientAddress, LoginTryResultEnum.Success);
                        EnviarResultado(client, SecondaryPasswordCheckEnum.CorrectOrSkipped);
                    }
                    else
                    {
                        await RegistrarTentativaLogin(account.Username, client.ClientAddress, LoginTryResultEnum.IncorrectSecondaryPassword);
                        EnviarResultado(client, SecondaryPasswordCheckEnum.Incorrect);
                    }
                }
                else
                {
                    await RegistrarTentativaLogin(account.Username, client.ClientAddress, LoginTryResultEnum.Success);
                    EnviarResultado(client, SecondaryPasswordCheckEnum.CorrectOrSkipped);
                }
            }
            catch (KeyNotFoundException ex)
            {
                _logger.Warning(ex, "Conta não encontrada: {Mensagem}", ex.Message);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Erro ao processar requisição de login: {Mensagem}", ex.Message);
                // Considere rethrow se necessário
            }
        }

        private async Task RegistrarTentativaLogin(string username, string clientAddress, LoginTryResultEnum resultado)
        {
            DebugLog($"Registrando tentativa de login para usuário {username} com resultado {resultado}.");
            await _sender.Send(new CreateLoginTryCommand(username, clientAddress, resultado));
        }

        private void EnviarResultado(GameClient client, SecondaryPasswordCheckEnum resultado)
        {
            DebugLog($"Enviando resultado da verificação de senha secundária: {resultado}.");
            client.Send(new SecondaryPasswordCheckResultPacket(resultado));
        }

    }
}
