using AutoMapper;
using DigitalWorldOnline.Account.Models.Configuration;
using DigitalWorldOnline.Application.Separar.Commands.Create;
using DigitalWorldOnline.Application.Separar.Commands.Delete;
using DigitalWorldOnline.Application.Separar.Commands.Update;
using DigitalWorldOnline.Application.Separar.Queries;
using DigitalWorldOnline.Commons.DTOs.Config;
using DigitalWorldOnline.Commons.Entities;
using DigitalWorldOnline.Commons.Enums.Account;
using DigitalWorldOnline.Commons.Enums.ClientEnums;
using DigitalWorldOnline.Commons.Enums.PacketProcessor;
using DigitalWorldOnline.Commons.Extensions;
using DigitalWorldOnline.Commons.Interfaces;
using DigitalWorldOnline.Commons.Models.Account;
using DigitalWorldOnline.Commons.Packet;
using DigitalWorldOnline.Commons.Packets.AuthenticationServer;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Serilog;
using System;
using System.Diagnostics;
using System.Text;


namespace DigitalWorldOnline.Account.PacketProcessors;

public class RecvLoginRequest : IAuthePacketProcessor
{
    public AuthenticationServerPacketEnum Type => AuthenticationServerPacketEnum.LoginRequest;

    private readonly ISender _sender;
    private readonly IConfiguration _configuration;
    private readonly IMapper _mapper;
    private readonly ILogger _logger;
    private readonly AuthenticationServerConfigurationModel? _authenticationServerConfiguration;

    public RecvLoginRequest(
        ISender sender,
        IConfiguration configuration,
        IMapper mapper,
        ILogger logger,
        IOptions<AuthenticationServerConfigurationModel>? authenticationServerOptions)
    {
        _sender = sender ?? throw new ArgumentNullException(nameof(sender));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _authenticationServerConfiguration = authenticationServerOptions?.Value;

        // Registra no log se a configuração não foi injetada corretamente
        if (_authenticationServerConfiguration == null)
        {
            _logger.Warning("AuthenticationServerConfigurationModel não foi injetado corretamente.");
        }
    }

    private void DebugLog(string message)
    {
        _logger.Debug($"{message}");
    }

    /// <summary>
    /// Processa a requisição de login do cliente.
    /// </summary>
    /// <param name="client">Cliente do jogo que enviou o pacote</param>
    /// <param name="packetData">Dados do pacote recebido (apenas payload, sem header)</param>
    /// <exception cref="ArgumentNullException">Lançada quando client ou packetData são nulos</exception>
    /// <returns>Task representando a operação assíncrona</returns>
    public async Task Process(GameClient client, byte[] packetData)
    {
        // Validação dos parâmetros de entrada
        ArgumentNullException.ThrowIfNull(client, nameof(client));
        ArgumentNullException.ThrowIfNull(packetData, nameof(packetData));

        try
        {
            // Cria um MemoryStream diretamente com os dados do payload
            using var stream = new MemoryStream(packetData);
            using var reader = new BinaryReader(stream);

            Debug.WriteLine($"Processing login packet with {packetData.Length} bytes of payload data");

            // Lê diretamente os dados do payload sem criar AuthenticationPacketReader
            var g_nNetVersion = BitConverter.ToUInt32(reader.ReadBytes(4), 0);
            var GetUserType = ExtractString(reader);
            var username = ExtractString(reader);
            var password = ExtractString(reader);
            var szCpuName = ExtractString(reader);
            var szGpuName = ExtractString(reader);
            var nPhyMemory = BitConverter.ToInt32(reader.ReadBytes(4), 0) / 1024;
            var szOS = ExtractString(reader);
            var szDxVersion = ExtractString(reader);

            // Debug para verificar se os valores estão sendo lidos corretamente
            _logger.Debug("Dados extraídos - Versão: {Version}, Tipo: {Type}, Usuário: {Username}",
                g_nNetVersion, GetUserType, username);

            // Registra início da validação de login no log
            _logger.Debug("Validando dados de login para {Usuario}", username);

            // Busca a conta pelo nome de usuário
            var conta = await _sender.Send(new AccountByUsernameQuery(username));

            // Verifica se a conta existe
            if (conta == null)
            {
                _logger.Debug("Registrando tentativa de login para {Usuario} com nome de usuário incorreto...", username);

                // Registra a tentativa de login mal-sucedida
                await _sender.Send(new CreateLoginTryCommand(username, client.ClientAddress, LoginTryResultEnum.IncorrectUsername));

                // Envia resposta de erro ao cliente
                client.Send(new LoginRequestAnswerPacket(LoginFailReasonEnum.UserNotFound));
                return;
            }

            // Define ID da conta e nível de acesso no cliente
            client.SetAccountId(conta.Id);
            client.SetAccessLevel(conta.AccessLevel);

            // Verifica se a conta está bloqueada
            if (conta.AccountBlock != null)
            {
                // Obtém informações do bloqueio
                var infoBloqueio = _mapper.Map<AccountBlockModel>(
                    await _sender.Send(new AccountBlockByIdQuery(conta.AccountBlock.Id)));

                // Verifica se o bloqueio ainda está ativo
                if (infoBloqueio.EndDate > DateTime.Now)
                {
                    // Calcula o tempo restante do bloqueio
                    TimeSpan tempoRestante = infoBloqueio.EndDate - DateTime.Now;
                    uint segundosRestantes = (uint)tempoRestante.TotalSeconds;

                    _logger.Debug("Registrando tentativa de login para {Usuario} com conta bloqueada...", username);

                    // Registra a tentativa de login
                    await _sender.Send(new CreateLoginTryCommand(username, client.ClientAddress,
                        LoginTryResultEnum.AccountBlocked));

                    // Envia informações do bloqueio ao cliente
                    client.Send(new LoginRequestBannedAnswerPacket(segundosRestantes, infoBloqueio.Reason));
                    return;
                }
                else
                {
                    // Remove o bloqueio expirado
                    await _sender.Send(new DeleteBanCommand(infoBloqueio.Id));
                }
            }

            // Verifica se a senha está correta
            if (conta.Password != password.Encrypt())
            {
                _logger.Debug("Registrando tentativa de login para {Usuario} com senha incorreta...", username);

                // Registra a tentativa de login mal-sucedida
                await _sender.Send(new CreateLoginTryCommand(username, client.ClientAddress,
                    LoginTryResultEnum.IncorrectPassword));

                // Envia resposta de erro ao cliente
                client.Send(new LoginRequestAnswerPacket(LoginFailReasonEnum.IncorrectPassword));
                return;
            }

            //// Envia o pacote solicitando configuração ou entrada da senha secundária, conforme necessário
            //var secPassScreen = conta.SecondaryPassword == null
            //    ? SecondaryPasswordScreenEnum.RequestSetup
            //    : SecondaryPasswordScreenEnum.RequestInput;


            var secPassScreen = SecondaryPasswordScreenEnum.Hide;

            client.Send(new LoginRequestAnswerPacket(secPassScreen));

            // Verifica se deve enviar hash de recursos
            if (_authenticationServerConfiguration?.UseHash == true)
            {
                _logger.Debug("Obtendo hash de recursos do banco de dados...");
                List<HashDTO> hashList = await _sender.Send(new ResourcesHashQuery());

                if (hashList != null && hashList.Any())
                {
                    _logger.Information("Recebidos {Count} hashes de recursos", hashList.Count);

                    // Verifica se a versão do cliente é a específica que estamos procurando (20031701)
                    if (g_nNetVersion == 20031701)
                    {
                        // Busca um hash com ClientVersion = 1
                        var clientVersionHash = hashList.FirstOrDefault(h => h.ClientVersion == 1);

                        if (clientVersionHash != null && !string.IsNullOrEmpty(clientVersionHash.Hash))
                        {
                            _logger.Debug("Enviando hash para o cliente com versão 20031701: {Hash}", clientVersionHash.Hash);
                            client.Send(new ResourcesHashPacket(clientVersionHash.Hash));
                        }
                        else
                        {
                            _logger.Warning("Hash de recursos para ClientVersion=1 não encontrado");
                        }
                    }
                    else if (g_nNetVersion == 22011101)
                    {
                        // Busca um hash com ClientVersion = 2
                        var clientVersionHash = hashList.FirstOrDefault(h => h.ClientVersion == 2);

                        if (clientVersionHash != null && !string.IsNullOrEmpty(clientVersionHash.Hash))
                        {
                            _logger.Debug("Enviando hash para o cliente com versão 22011101: {Hash}", clientVersionHash.Hash);
                            client.Send(new ResourcesHashPacket(clientVersionHash.Hash));
                        }
                        else
                        {
                            _logger.Warning("Hash de recursos para ClientVersion=2 não encontrado");
                        }
                    }
                    else
                    {
                        _logger.Debug("Versão do cliente {ClientVersion} não corresponde às versões conhecidas", g_nNetVersion);
                    }
                }
                else
                {
                    _logger.Warning("Nenhum hash de recurso disponível no banco de dados");
                }
            }

            // Atualiza ou cria informações do sistema do usuário
            if (conta.SystemInformation == null)
            {
                _logger.Debug("Criando informações do sistema para {Usuario}...", username);

                // Cria novas informações do sistema
                await _sender.Send(
                    new CreateSystemInformationCommand(conta.Id, szCpuName, szGpuName, client.ClientAddress));
            }
            else
            {
                _logger.Debug("Atualizando informações do sistema para {Usuario}...", username);

                // Atualiza informações existentes
                await _sender.Send(new UpdateSystemInformationCommand(conta.SystemInformation.Id, conta.Id,
                    szCpuName, szGpuName, client.ClientAddress));
            }
        }
        catch (Exception ex)
        {
            // Registra o erro no log
            _logger.Error(ex, "Erro ao processar requisição de login: {Mensagem}", ex.Message);
        }
    }

    /// <summary>
    /// Extrai uma string do BinaryReader, lidando com o formato específico dos dados.
    /// </summary>
    /// <param name="reader">O BinaryReader para ler os dados</param>
    /// <returns>A string extraída</returns>
    /// <exception cref="InvalidDataException">Lançada quando o tamanho da string é inválido</exception>
    /// <exception cref="InvalidOperationException">Lançada quando ocorre um erro ao extrair os dados</exception>
    private static string ExtractString(BinaryReader reader)
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
}