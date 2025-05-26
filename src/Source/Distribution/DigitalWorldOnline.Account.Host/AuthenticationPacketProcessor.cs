using DigitalWorldOnline.Application.Separar.Commands.Create;
using DigitalWorldOnline.Application.Separar.Commands.Update;
using DigitalWorldOnline.Application.Separar.Commands.Delete;
using DigitalWorldOnline.Application.Separar.Queries;
using DigitalWorldOnline.Commons.Entities;
using DigitalWorldOnline.Commons.Enums.Account;
using DigitalWorldOnline.Commons.Enums.ClientEnums;
using DigitalWorldOnline.Commons.Enums.PacketProcessor;
using DigitalWorldOnline.Commons.Extensions;
using DigitalWorldOnline.Commons.Interfaces;
using DigitalWorldOnline.Commons.Models.Account;
using DigitalWorldOnline.Commons.Models.Servers;
using DigitalWorldOnline.Commons.Packets.AuthenticationServer;
using DigitalWorldOnline.Account.Models.Configuration;
using MediatR;
using Microsoft.Extensions.Configuration;
using Serilog;
using System.Text;
using DigitalWorldOnline.Application.Admin.Commands;
using Microsoft.Extensions.Options;
using System.Security;
using AutoMapper;

namespace DigitalWorldOnline.Account
{
    public sealed class AuthenticationPacketProcessor : IProcessor, IDisposable
    {
        private readonly IConfiguration _configuration;               // Configurações da aplicação
        private readonly IMapper _mapper;                            // AutoMapper para mapeamento de objetos
        private readonly ISender _sender;                            // MediatR para enviar comandos/consultas
        private readonly ILogger _logger;                            // Serilog para logging
        private readonly AuthenticationServerConfigurationModel _authenticationServerConfiguration;  // Configurações do servidor
        private readonly IEnumerable<IAuthePacketProcessor> _packetProcessors;

        private const string CharacterServerAddress = "CharacterServer:Address";

        public AuthenticationPacketProcessor(
            IEnumerable<IAuthePacketProcessor> packetProcessors,
            IMapper mapper, 
            ILogger logger, 
            ISender sender,
            IConfiguration configuration,
            IOptions<AuthenticationServerConfigurationModel> authenticationServerConfiguration)
        {
            _packetProcessors = packetProcessors;
            _configuration = configuration;
            _authenticationServerConfiguration = authenticationServerConfiguration.Value;
            _mapper = mapper;
            _sender = sender;
            _logger = logger;
        }

        private const int HandshakeDegree = 32321;

        /// <summary>
        /// Processa pacotes TCP recebidos do cliente do jogo.
        /// </summary>
        /// <param name="client">O cliente do jogo que enviou o pacote</param>
        /// <param name="data">Os bytes do pacote</param>
        /// <returns>Uma tarefa representando a operação assíncrona</returns>
        /// <summary>
        /// Processa pacotes TCP recebidos do cliente do jogo.
        /// </summary>
        /// <param name="client">O cliente do jogo que enviou o pacote</param>
        /// <param name="data">Os bytes do pacote</param>
        /// <returns>Uma tarefa representando a operação assíncrona</returns>
        public async Task ProcessPacketAsync(GameClient client, byte[] data)
        {
            ArgumentNullException.ThrowIfNull(client, nameof(client));
            ArgumentNullException.ThrowIfNull(data, nameof(data));

            if (data.Length < 4)
            {
                _logger?.Warning("Pacote recebido muito pequeno para ser válido. Tamanho: {Length}", data.Length);
                return;
            }

            try
            {
                // Passa o cliente para o construtor do PacketReader para permitir desconexão quando necessário
                var packet = new AuthenticationPacketReader(data, client);

                // Se o pacote não for válido e não for um pacote de conexão, ignoramos o processamento
                if (!packet.IsValid && packet.Enum != AuthenticationServerPacketEnum.Connection)
                {
                    _logger?.Warning("Ignorando pacote inválido tipo {Type} de {Address}",
                        packet.Enum, client.ClientAddress);
                    return;
                }

                _logger?.Debug("Pacote recebido tipo {Type} de {Address}", packet.Enum, client.ClientAddress);

                // Tratamento especial para pacotes Unknown
                if (packet.Enum == AuthenticationServerPacketEnum.Unknown)
                {
                    _logger?.Warning("Pacote desconhecido. Tipo: {Type}, Tamanho: {Length}, Cliente: {Address}",
                        packet.Type, packet.Length, client.ClientAddress);
                    return;
                }

                // Busca o processador correspondente
                var processor = _packetProcessors?.FirstOrDefault(x => x.Type == packet.Enum);

                if (processor != null)
                {
                    // Executa o processador correspondente
                    try
                    {
                        await processor.Process(client, data);
                        _logger?.Debug("Processado pacote {Type} com sucesso", packet.Enum);
                    }
                    catch (Exception ex)
                    {
                        _logger?.Error(ex, "Erro ao processar pacote {Type}: {Message}", packet.Enum, ex.Message);
                    }
                }
                else
                {
                    // Tratamento de fallback para tipos de pacotes específicos que não têm processador dedicado
                    switch (packet.Enum)
                    {
                        case AuthenticationServerPacketEnum.Connection:
                            await HandleConnectionPacket(client, packet);
                            break;

                        case AuthenticationServerPacketEnum.KeepConnection:
                            // Apenas reconhece o pacote de keepalive, não requer processamento
                            _logger?.Debug("Keepalive recebido de {Address}", client.ClientAddress);
                            break;

                        default:
                            _logger?.Warning("Nenhum processador encontrado para o pacote tipo {Type} do cliente {Address}",
                                packet.Enum, client.ClientAddress);
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.Error(ex, "Erro ao analisar ou processar pacote de {Address}: {Message}",
                    client.ClientAddress, ex.Message);

                // Se houver um erro grave na análise do pacote, desconecta o cliente
                // exceto para erros conhecidos de checksum que já são tratados no AuthenticationPacketReader
                if (!(ex.Message?.Contains("Checksum inválido") ?? false))
                {
                    _logger?.Warning("Desconectando cliente {ClientAddress} devido a erro de processamento",
                        client.ClientAddress);
                    client.Disconnect();
                }
            }
        }

        /// <summary>
        /// Converte um valor Int64 (long) para uma string de endereço MAC formatada
        /// </summary>
        /// <param name="macAddressLong">Valor long representando o endereço MAC</param>
        /// <returns>String formatada do endereço MAC (formato XX:XX:XX:XX:XX:XX)</returns>
        private string ConvertMacAddressToString(long macAddressLong)
        {
            // Os bytes do MAC estão contidos no valor Int64
            byte[] allBytes = BitConverter.GetBytes(macAddressLong);

            // Log dos bytes para depuração
            _logger?.Debug("MAC bytes: {0}", BitConverter.ToString(allBytes));

            // Supondo que a ordem real dos bytes é a ordem em que aparecem no valor Int64
            // Formato a partir da ordem original dos bytes
            if (BitConverter.IsLittleEndian)
            {
                // No formato little-endian, os bytes estão invertidos
                // Para obter o MAC original "8C:B0:E9:D3:A9:42" precisamos converter
                return string.Format("{0:X2}:{1:X2}:{2:X2}:{3:X2}:{4:X2}:{5:X2}",
                    allBytes[0], allBytes[1], allBytes[2],
                    allBytes[3], allBytes[4], allBytes[5]);
            }
            else
            {
                // No formato big-endian, precisamos selecionar os bytes corretos
                // Se os últimos dois bytes forem zeros (como 00 00 no exemplo)
                return string.Format("{0:X2}:{1:X2}:{2:X2}:{3:X2}:{4:X2}:{5:X2}",
                    allBytes[2], allBytes[3], allBytes[4],
                    allBytes[5], allBytes[6], allBytes[7]);
            }
        }

        /// <summary>
        /// Manipula pacotes de conexão quando não há um processador dedicado disponível.
        /// </summary>
        private async Task HandleConnectionPacket(GameClient client, AuthenticationPacketReader packet)
        {
            try
            {
                var macAdress = ConvertMacAddressToString(packet.ReadInt64());
                var Timestamp = packet.ReadUInt();
                var palavraCodificada = packet.ReadUShort();
                var IdCliente = packet.ReadInt();
                var CobyteCodificado1 = packet.ReadByte();
                var CobyteCodificado2 = packet.ReadByte();

                var handshakeTimestamp = (uint)DateTimeOffset.Now.ToUnixTimeSeconds();
                var handshake = (short)(client.Handshake ^ HandshakeDegree);

                _logger?.Debug("Enviando resposta de conexão para {Address}", client.ClientAddress);
                client.Send(new ConnectionPacket(handshake, handshakeTimestamp));
            }
            catch (Exception ex)
            {
                _logger?.Error(ex, "Erro ao processar pacote de conexão: {Message}", ex.Message);
            }
        }
        /// <summary>       
        /// Shortcut for debug logging with client and packet info.
        /// </summary>
        /// <param name="message">The message to log</param>
        private void DebugLog(string message)
        {
            _logger?.Debug($"{message}");
        }

        /// <summary>
        /// Disposes the entire object.
        /// </summary>
        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }
}