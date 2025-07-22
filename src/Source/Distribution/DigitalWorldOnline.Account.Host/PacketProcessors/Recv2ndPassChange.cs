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
        private static string ExtractSecondaryPassword(BinaryReader reader)
        {
            ArgumentNullException.ThrowIfNull(reader, nameof(reader));

            try
            {
                // Lê o tamanho da string (2 bytes - short)
                byte[] sizeBytes = reader.ReadBytes(2);
                int size = BitConverter.ToInt16(sizeBytes, 0);

                // Verifica se o tamanho é válido
                if (size < 0)
                    throw new InvalidDataException("Valor do tamanho inválido: não pode ser negativo");

                // Caso o tamanho seja zero, retorna string vazia
                if (size == 0)
                    return string.Empty;

                // Lê os bytes da string apenas se o tamanho for maior que zero
                byte[] stringBytes = reader.ReadBytes(size);

                // Converte os bytes para string usando ASCII e remove caracteres nulos
                string result = Encoding.ASCII.GetString(stringBytes).TrimEnd('\0');

                return result;
            }
            catch (Exception ex) when (ex is not InvalidDataException && ex is not ArgumentNullException)
            {
                // Captura e relança exceções com informações de contexto adicionais
                throw new InvalidOperationException("Falha ao extrair dados do pacote de autenticação", ex);
            }
        }

        public async Task Process(GameClient client, byte[] packetData)
        {
            ArgumentNullException.ThrowIfNull(client, nameof(client));
            ArgumentNullException.ThrowIfNull(packetData, nameof(packetData));

            try
            {
                using var stream = new MemoryStream(packetData);
                using var reader = new BinaryReader(stream);

                DebugLog("Processando solicitação de alteração de senha secundária");
                var currentSecurityCode = ExtractSecondaryPassword(reader);
                var newSecurityCode = ExtractSecondaryPassword(reader);

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