using AutoMapper;
using DigitalWorldOnline.Account.Models.Configuration;
using DigitalWorldOnline.Application.Admin.Commands;
using DigitalWorldOnline.Application.Separar.Commands.Create;
using DigitalWorldOnline.Application.Separar.Commands.Delete;
using DigitalWorldOnline.Application.Separar.Commands.Update;
using DigitalWorldOnline.Application.Separar.Queries;
using DigitalWorldOnline.Commons.Entities;
using DigitalWorldOnline.Commons.Enums.Account;
using DigitalWorldOnline.Commons.Enums.ClientEnums;
using DigitalWorldOnline.Commons.Enums.PacketProcessor;
using DigitalWorldOnline.Commons.Extensions;
using DigitalWorldOnline.Commons.Interfaces;
using DigitalWorldOnline.Commons.Models.Account;
using DigitalWorldOnline.Commons.Models.Servers;
using DigitalWorldOnline.Commons.Packet;
using DigitalWorldOnline.Commons.Packets.AuthenticationServer;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Serilog;
using System;
using System.Security;
using System.Text;

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
        public async Task ProcessPacketAsync(GameClient client, byte[] data)
        {
            ArgumentNullException.ThrowIfNull(client, nameof(client));
            ArgumentNullException.ThrowIfNull(data, nameof(data));

            if (data.Length < 4)
            {
                _logger?.Warning("Pacote recebido muito pequeno para ser válido. Tamanho: {Length}", data.Length);
                return;
            }

            if (data == null || data.Length == 0)
            {
                _logger?.Warning($"Pacote vazio recebido de {client.ClientAddress}. Desconectando cliente.");
                return;
            }

            try
            {
                // Passa o cliente para o construtor do PacketReader para permitir desconexão quando necessário
                var packet = new AuthenticationPacketReader(data, client);

                PacketReaderExtensions.SaveAsync(data, packet.Type, packet.Length).Wait();

                // Log do hex dump tanto no console quanto nos arquivos
                string hexDumpOutput = $"RECV [{client.ClientAddress}] Tipo: {packet.Type} ({packet.Enum}) | Tamanho: {packet.Length}\r\n{Dump.HexDump(data, packet.Length)}";

                // Exibe no console com cores
                //DisplayPacketHexInConsole(client.ClientAddress, packet.Type, packet.Enum.ToString(), packet.Length, data);

                // Log nos arquivos
                SysCons.LogPacketRecv(hexDumpOutput);

                // Se o pacote não for válido e não for um pacote de conexão, ignoramos o processamento
                if (!packet.IsValid && packet.Enum != AuthenticationServerPacketEnum.Connection)
                {
                    _logger?.Warning("Ignorando pacote inválido tipo {Type} de {Address}",
                        packet.Enum, client.ClientAddress);
                    return;
                }
                // Se o pacote não for válido e não for um pacote de conexão, ignoramos o processamento
                if (!packet.IsValid && packet.Enum != AuthenticationServerPacketEnum.KeepConnection)
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
                    // Extrai apenas os dados do payload, removendo header e checksum
                    byte[] payloadData = ExtractPayloadData(data, packet.Length,packet.Type);

                    // Executa o processador correspondente com apenas os dados úteis
                    try
                    {
                        await processor.Process(client, payloadData);
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
        /// Exibe o hex dump do pacote no console com formatação colorida.
        /// </summary>
        /// <param name="clientAddress">Endereço do cliente</param>
        /// <param name="packetType">Tipo do pacote (valor numérico)</param>
        /// <param name="packetEnum">Nome do enum do pacote</param>
        /// <param name="packetLength">Tamanho do pacote</param>
        /// <param name="data">Dados do pacote</param>
        private void DisplayPacketHexInConsole(string clientAddress, int packetType, string packetEnum, int packetLength, byte[] data)
        {
            try
            {
                string timestamp = DateTime.Now.ToString("HH:mm:ss");
                string hexDump = Dump.HexDump(data, packetLength);

                // Usar lock para evitar que diferentes threads misturem saídas do console
                lock (Console.Out)
                {
                    // Header do pacote
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.Write($"[{timestamp}] ");
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.Write($"[PACKET-RECV] ");
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.Write($"Cliente: ");
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.Write($"{clientAddress} ");
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.Write($"| Tipo: ");
                    Console.ForegroundColor = ConsoleColor.Magenta;
                    Console.Write($"{packetType} ");
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.Write($"({packetEnum}) | Tamanho: ");
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"{packetLength} bytes");

                    // Hex dump
                    Console.ForegroundColor = ConsoleColor.Gray;
                    Console.WriteLine(hexDump);

                    // Linha separadora
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine(new string('-', 80));

                    Console.ResetColor();
                }
            }
            catch (Exception ex)
            {
                // Fallback em caso de erro na exibição
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[ERRO AO EXIBIR HEX] {ex.Message}");
                Console.ResetColor();
            }
        }

        /// <summary>
        /// Extrai apenas os dados do payload do pacote, removendo o header e checksum.
        /// </summary>
        /// <param name="rawData">Dados brutos do pacote</param>
        /// <param name="packetLength">Tamanho declarado do pacote</param>
        /// <returns>Array de bytes contendo apenas os dados do payload</returns>
        private byte[] ExtractPayloadData(byte[] rawData, int packetLength,int type)
        {
            // Estrutura do pacote:
            // [4 bytes: Length] [2 bytes: Type] [payload data] [4 bytes: Checksum]

            const int headerSize = 6; // Length (4) + Type (2)
            const int checksumSize = 4; // Checksum (4)

            // Calcula o tamanho do payload (dados úteis)
            int payloadSize = packetLength - headerSize - checksumSize;

            // Verifica se o tamanho é válido e exibe informações adicionais no console
            if (payloadSize < 0 || headerSize + payloadSize > rawData.Length)
            {
                _logger?.Warning("Tamanho de payload inválido: {PayloadSize}, tamanho do pacote: {PacketLength},Type: {type}",
                    payloadSize, packetLength);

                // Exibe aviso também no console
                lock (Console.Out)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"[AVISO] Payload inválido - Type: {type}, Size: {payloadSize}, PacketLength: {packetLength}, RawDataLength: {rawData.Length}");
                    Console.ResetColor();
                }

                return Array.Empty<byte>();
            }

            // Extrai apenas os dados do payload
            byte[] payload = new byte[payloadSize];
            Array.Copy(rawData, headerSize, payload, 0, payloadSize);

            _logger?.Debug("Payload extraído: {PayloadSize} bytes de um pacote de {PacketLength} bytes",
                payloadSize, packetLength);

            return payload;
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