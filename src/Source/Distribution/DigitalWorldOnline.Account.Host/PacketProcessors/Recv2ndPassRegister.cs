using AutoMapper;
using DigitalWorldOnline.Account.Models.Configuration;
using DigitalWorldOnline.Application.Separar.Commands.Create;
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
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace DigitalWorldOnline.Account.PacketProcessors;

internal class Recv2ndPassRegister : IAuthePacketProcessor
{
    public AuthenticationServerPacketEnum Type => AuthenticationServerPacketEnum.SecondaryPasswordRegister;

    private readonly ISender _sender;
    private readonly IMapper _mapper;
    private readonly ILogger _logger;
    private readonly IConfiguration _configuration;
    private readonly AuthenticationServerConfigurationModel _authenticationServerConfiguration;


    public Recv2ndPassRegister(
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

    public static string ExtractMD5Hash(AuthenticationPacketReader packet) => ExtractData(packet);


    private static string ExtractData(AuthenticationPacketReader packet)
    {
        int size = 32;
        string data = Encoding.ASCII.GetString(packet.ReadBytes(size)).Trim();
        int sizenull = packet.ReadByte();
        return data;
    }

    public async Task Process(GameClient client, byte[] packetData)
    {
        // Validação dos parâmetros de entrada
        ArgumentNullException.ThrowIfNull(client, nameof(client));
        ArgumentNullException.ThrowIfNull(packetData, nameof(packetData));

        try
        {
            // Cria um leitor de pacotes para processar os dados recebidos
            var packet = new AuthenticationPacketReader(packetData);

            DebugLog("Leitura de parâmetros de pacotes...");
            var securityPassword = ExtractMD5Hash(packet);

            DebugLog($"Atualizando {client.AccountId} Informações da conta ...");
            await _sender.Send(new CreateOrUpdateSecondaryPasswordCommand(client.AccountId, securityPassword));

            client.Send(new LoginRequestAnswerPacket(SecondaryPasswordScreenEnum.RequestInput));
        }
        catch (Exception ex)
        {
            // Registra o erro no log
            _logger.Error(ex, "Erro ao processar requisição de login: {Mensagem}", ex.Message);
        }
    }
}
