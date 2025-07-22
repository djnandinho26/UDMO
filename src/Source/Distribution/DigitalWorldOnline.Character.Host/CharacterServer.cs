using DigitalWorldOnline.Commons.Entities;
using DigitalWorldOnline.Commons.Interfaces;
using DigitalWorldOnline.Commons.Packets.CharacterServer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Text;

namespace DigitalWorldOnline.Character
{
    public sealed class CharacterServer : GameServer, IHostedService
    {
        #region Fields and Constants

        private readonly IHostApplicationLifetime _hostApplicationLifetime;
        private readonly IConfiguration _configuration;
        private readonly IProcessor _processor;
        private readonly ILogger _logger;
        private readonly Timer? _cleanupTimer;

        // Constantes de configuração
        private const int MaxConcurrentConnections = 300;
        private const int MinTimeBetweenConnections = 1; // segundos
        private const int BlockDurationMinutes = 20;
        private const int OnConnectEventHandshakeHandler = 65535;

        // Dicionários para controle de conexões e bloqueios
        private readonly ConcurrentDictionary<string, DateTime> _lastConnectionAttempts;
        private readonly ConcurrentDictionary<string, DateTime> _temporarilyBlockedIps;

        #endregion

        #region Constructor

        public CharacterServer(
            IHostApplicationLifetime hostApplicationLifetime,
            IConfiguration configuration,
            IProcessor processor,
            ILogger logger)
        {
            // Inicialização dos dicionários - CRÍTICO para evitar NullReferenceException
            _lastConnectionAttempts = new ConcurrentDictionary<string, DateTime>();
            _temporarilyBlockedIps = new ConcurrentDictionary<string, DateTime>();

            // Configuração das dependências
            _hostApplicationLifetime = hostApplicationLifetime;
            _configuration = configuration;
            _processor = processor;
            _logger = logger;

            // Configuração dos eventos
            OnConnect += OnConnectEvent;
            OnDisconnect += OnDisconnectEvent;
            DataReceived += OnDataReceivedEvent;

            // Inicialização do timer de limpeza
            _cleanupTimer = new Timer(CleanupBlockedIps, null,
                TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
        }

        #endregion

        #region Connection Management

        /// <summary>
        /// Sobrescreve o método OnClientConnection para controlar conexões e implementar proteção contra spam
        /// </summary>
        public override void OnClientConnection(GameClientEvent e)
        {
            if (e?.Client?.ClientAddress == null)
            {
                _logger.Warning("Tentativa de conexão com dados inválidos rejeitada.");
                return;
            }

            // Extrai o IP do cliente sem a porta
            string clientIp = ExtractClientIp(e.Client.ClientAddress);

            try
            {
                // Verifica se a conexão deve ser bloqueada
                if (ShouldBlockConnection(clientIp))
                {
                    RejectConnection(e.Client, clientIp, "IP bloqueado ou tentando reconectar muito rapidamente");
                    return;
                }

                // Verifica limite de conexões simultâneas
                if (Clients.Count >= MaxConcurrentConnections)
                {
                    RejectConnection(e.Client, clientIp, $"Limite de {MaxConcurrentConnections} conexões atingido");
                    return;
                }

                // Conexão aceita
                _logger.Debug($"Conexão aceita de {clientIp}. Conexões ativas: {Clients.Count + 1}/{MaxConcurrentConnections}");
                base.OnClientConnection(e);
            }
            catch (Exception ex)
            {
                _logger.Error($"Erro ao processar conexão de {clientIp}: {ex.Message}");
                RejectConnection(e.Client, clientIp, "Erro interno do servidor");
            }
        }

        /// <summary>
        /// Extrai o endereço IP da string de endereço do cliente
        /// </summary>
        private static string ExtractClientIp(string clientAddress)
        {
            if (string.IsNullOrEmpty(clientAddress))
                return "Desconhecido";

            var parts = clientAddress.Split(':');
            return parts.Length > 0 ? parts[0] : "Desconhecido";
        }

        /// <summary>
        /// Rejeita uma conexão e fecha o socket
        /// </summary>
        private void RejectConnection(GameClient client, string clientIp, string reason)
        {
            _logger.Warning($"Conexão rejeitada de {clientIp}: {reason}");

            try
            {
                var message = $"Conexão rejeitada: {reason}. Tente novamente mais tarde.\n";
                client.Socket.Send(Encoding.UTF8.GetBytes(message));
                client.Socket.Shutdown(SocketShutdown.Both);
                client.Socket.Close();
            }
            catch (Exception ex)
            {
                _logger.Error($"Erro ao fechar conexão rejeitada de {clientIp}: {ex.Message}");
            }
            finally
            {
                RemoveClient(client, false);
            }
        }

        #endregion

        #region IP Blocking Logic

        /// <summary>
        /// Verifica se uma conexão deve ser bloqueada com base na frequência
        /// </summary>
        private bool ShouldBlockConnection(string ipAddress)
        {
            if (string.IsNullOrEmpty(ipAddress) || ipAddress == "Desconhecido")
                return false;

            var now = DateTime.Now;

            // Verifica se o IP já está bloqueado
            if (IsIpBlocked(ipAddress))
                return true;

            // Verifica frequência de conexões
            if (_lastConnectionAttempts.TryGetValue(ipAddress, out DateTime lastAttempt))
            {
                var timeSinceLastAttempt = now - lastAttempt;

                if (timeSinceLastAttempt.TotalSeconds < MinTimeBetweenConnections)
                {
                    // Bloqueia o IP
                    var blockUntil = now.AddMinutes(BlockDurationMinutes);
                    _temporarilyBlockedIps.TryAdd(ipAddress, blockUntil);

                    _logger.Warning($"IP {ipAddress} bloqueado por {BlockDurationMinutes} minutos " +
                                  $"por tentar reconectar em menos de {MinTimeBetweenConnections} segundos");
                    return true;
                }
            }

            // Atualiza o timestamp da última conexão
            _lastConnectionAttempts.AddOrUpdate(ipAddress, now, (key, oldValue) => now);
            return false;
        }

        /// <summary>
        /// Verifica se um IP está atualmente bloqueado
        /// </summary>
        private bool IsIpBlocked(string ipAddress)
        {
            if (string.IsNullOrEmpty(ipAddress))
                return false;

            if (_temporarilyBlockedIps.TryGetValue(ipAddress, out DateTime blockUntil))
            {
                if (DateTime.Now < blockUntil)
                    return true;

                // Bloqueio expirou, remove da lista
                _temporarilyBlockedIps.TryRemove(ipAddress, out _);
            }

            return false;
        }

        /// <summary>
        /// Remove IPs bloqueados que já expiraram
        /// </summary>
        private void CleanupBlockedIps(object? state)
        {
            try
            {
                var now = DateTime.Now;
                var expiredIps = _temporarilyBlockedIps
                    .Where(pair => now > pair.Value)
                    .Select(pair => pair.Key)
                    .ToList();

                foreach (var ip in expiredIps)
                {
                    if (_temporarilyBlockedIps.TryRemove(ip, out _))
                    {
                        _logger.Debug($"IP {ip} desbloqueado após expiração do período de bloqueio");
                    }
                }

                // Limpa conexões antigas (mais de 1 hora)
                var oldConnections = _lastConnectionAttempts
                    .Where(pair => (now - pair.Value).TotalHours > 1)
                    .Select(pair => pair.Key)
                    .ToList();

                foreach (var ip in oldConnections)
                {
                    _lastConnectionAttempts.TryRemove(ip, out _);
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Erro durante limpeza de IPs bloqueados: {ex.Message}");
            }
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// Evento disparado quando um cliente se conecta
        /// </summary>
        private void OnConnectEvent(object sender, GameClientEvent gameClientEvent)
        {
            try
            {
                var clientIp = ExtractClientIp(gameClientEvent.Client.ClientAddress ?? "");

                _logger.Information($"Evento de conexão aceito de {gameClientEvent.Client.HiddenAddress}");

                // Configura handshake
                var handshake = (short)(DateTimeOffset.Now.ToUnixTimeSeconds() & OnConnectEventHandshakeHandler);
                gameClientEvent.Client.SetHandshake(handshake);

                if (gameClientEvent.Client.IsConnected)
                {
                    _logger.Debug($"Enviando handshake para {gameClientEvent.Client.ClientAddress}");
                    gameClientEvent.Client.Send(new OnConnectEventConnectionPacket(handshake));
                }
                else
                {
                    _logger.Warning($"Cliente {gameClientEvent.Client.ClientAddress} desconectado antes do handshake");
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Erro no evento de conexão: {ex.Message}");
            }
        }

        /// <summary>
        /// Evento disparado quando um cliente se desconecta
        /// </summary>
        private void OnDisconnectEvent(object sender, GameClientEvent gameClientEvent)
        {
            try
            {
                if (!string.IsNullOrEmpty(gameClientEvent.Client.ClientAddress))
                {
                    _logger.Information($"Evento de desconexão de {gameClientEvent.Client.HiddenAddress}");
                    _logger.Debug($"Cliente desconectado: {gameClientEvent.Client.ClientAddress}. " +
                                $"Account: {gameClientEvent.Client.AccountId}");
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Erro no evento de desconexão: {ex.Message}");
            }
        }

        /// <summary>
        /// Evento disparado quando dados são recebidos de um cliente
        /// </summary>
        private void OnDataReceivedEvent(object sender, GameClientEvent gameClientEvent, byte[] data)
        {
            try
            {
                // Validações básicas
                if (gameClientEvent?.Client == null)
                {
                    _logger.Error("GameClientEvent ou Cliente é nulo no OnDataReceivedEvent");
                    return;
                }

                if (data == null || data.Length == 0)
                {
                    _logger.Warning($"Dados inválidos recebidos de {gameClientEvent.Client.ClientAddress}");
                    return;
                }

                _logger.Debug($"Recebidos {data.Length} bytes de {gameClientEvent.Client.ClientAddress}");

                // Processa os pacotes
                ProcessPackets(gameClientEvent, data);
            }
            catch (Exception ex)
            {
                _logger.Error($"Erro ao processar dados recebidos: {ex.Message}");
                HandleClientError(gameClientEvent, data, ex);
            }
        }

        #endregion

        #region Packet Processing

        /// <summary>
        /// Processa os pacotes recebidos do cliente
        /// </summary>
        private void ProcessPackets(GameClientEvent gameClientEvent, byte[] data)
        {
            try
            {
                // Verificar se o evento, cliente ou dados são nulos
                if (gameClientEvent == null)
                {
                    _logger.Error("GameClientEvent é nulo no OnDataReceivedEvent");
                    return;
                }

                if (gameClientEvent.Client == null)
                {
                    _logger.Error("Cliente é nulo no OnDataReceivedEvent");
                    return;
                }

                if (data == null)
                {
                    _logger.Error($"Dados recebidos são nulos de {gameClientEvent.Client.ClientAddress}");
                    return;
                }

                // Verificar se os dados têm tamanho válido
                if (data.Length <= 0)
                {
                    _logger.Warning($"Dados recebidos com tamanho inválido ({data.Length}) de {gameClientEvent.Client.ClientAddress}");
                    return;
                }

                _logger.Debug($"Recebido {data.Length} bytes de {gameClientEvent.Client.ClientAddress}.");

                // Buffer para processar os dados recebidos
                int offset = 0;
                int processedPackets = 0;
                const int maxPacketsPerBatch = 512; // Limite de pacotes por lote

                while (offset < data.Length && processedPackets < maxPacketsPerBatch)
                {
                    // Verificar se temos bytes suficientes para ler o tamanho do pacote
                    if (data.Length - offset < 2)
                    {
                        _logger.Warning($"Pacote incompleto recebido de {gameClientEvent.Client.ClientAddress}. Bytes restantes: {data.Length - offset}");
                        return;
                    }

                    // Ler o tamanho do pacote (primeiros 2 bytes em little-endian)
                    int packetSize = BitConverter.ToInt32(data, offset);

                    // Se encontrar um pacote com tamanho 0, registra e SAIR completamente do processamento
                    if (packetSize <= 0)
                    {
                        //_logger.Warning($"Pacote com tamanho 0 recebido de {gameClientEvent.Client.ClientAddress}. Interrompendo processamento.");
                        return; // Sai imediatamente do método
                    }

                    // Verificar se o tamanho do pacote é válido
                    if (packetSize <= 0 || packetSize > 131072)
                    {
                        //_logger.Warning($"Tamanho de pacote inválido ({packetSize}) recebido de {gameClientEvent.Client.ClientAddress}");
                        return;
                    }

                    // Verificar se temos o pacote completo
                    if (offset + packetSize > data.Length)
                    {
                        _logger.Warning($"Pacote incompleto recebido de {gameClientEvent.Client.ClientAddress}. Esperado: {packetSize}, Disponível: {data.Length - offset}");
                        return;
                    }

                    // Verificar se há bytes suficientes para ler o checksum (se necessário)
                    int checksumIndex = offset + (packetSize - 4);
                    bool checksumValid = true;

                    // Verificar se o índice do checksum está dentro dos limites do array
                    if (checksumIndex >= 0 && checksumIndex < data.Length - 1)
                    {
                        try
                        {
                            int packetChecksum = BitConverter.ToInt32(data, checksumIndex);

                            // Se o checksum for 0, ignorar o pacote
                            if (packetChecksum <= 0)
                            {
                                //_logger.Warning($"Pacote com checksum 0 recebido de {gameClientEvent.Client.ClientAddress}. Interrompendo processamento.");
                                return;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.Warning($"Erro ao ler checksum: {ex.Message}. Continuando processamento.");
                            checksumValid = false;
                        }
                    }
                    else
                    {
                        // Índice do checksum está fora dos limites
                        checksumValid = false;
                    }

                    // Criar um buffer para o pacote atual
                    byte[] packetData = new byte[packetSize];
                    Array.Copy(data, offset, packetData, 0, packetSize);

                    try
                    {
                        // Processar o pacote apenas se for válido
                        if (checksumValid)
                        {
                            _logger.Debug($"Processando pacote de {packetSize} bytes de {gameClientEvent.Client.ClientAddress}");

                            // Verificação extra antes de chamar ProcessPacketAsync
                            if (_processor != null && gameClientEvent.Client != null && packetData != null && packetData.Length > 0)
                            {
                                _processor.ProcessPacketAsync(gameClientEvent.Client, packetData);
                            }
                            else
                            {
                                _logger.Warning($"Impossível processar pacote: _processor={_processor != null}, cliente={gameClientEvent.Client != null}, dados válidos={packetData != null && packetData.Length > 0}");
                            }
                        }
                        else
                        {
                            //_logger.Warning($"Ignorando pacote com checksum inválido de {gameClientEvent.Client.ClientAddress}");
                        }
                    }
                    catch (Exception procEx)
                    {
                        _logger.Error($"Erro ao processar pacote: {procEx.Message}");
                        // Não interrompe o processamento dos outros pacotes
                    }

                    // Avançar o offset para o próximo pacote
                    offset += packetSize;
                    processedPackets++;
                }
            }
            catch (Exception ex)
            {
                // Verificar se o cliente é válido antes de tentar desconectá-lo
                if (gameClientEvent?.Client != null)
                {
                    try
                    {
                        gameClientEvent.Client.SetGameQuit(true);
                        gameClientEvent.Client.Disconnect();
                    }
                    catch (Exception disconnectEx)
                    {
                        _logger.Error($"Erro ao desconectar cliente após falha: {disconnectEx.Message}");
                    }
                }

                _logger.Error($"Erro de pacote de processo: {ex.Message} {ex.InnerException} {ex.StackTrace}.");

                try
                {
                    if (gameClientEvent?.Client != null && data != null)
                    {
                        var filePath = $"PacketErrors/{gameClientEvent.Client.AccountId}_{DateTime.Now:dd_MM_HH_mm_ss}.txt";

                        Directory.CreateDirectory("PacketErrors");

                        using var fs = File.Create(filePath);
                        fs.Write(data, 0, data.Length);
                    }
                }
                catch (Exception fileEx)
                {
                    _logger.Error($"Erro ao salvar arquivo de erro de pacote: {fileEx.Message}");
                }
            }
        }

        /// <summary>
        /// Trata erros relacionados ao cliente
        /// </summary>
        private void HandleClientError(GameClientEvent gameClientEvent, byte[] data, Exception ex)
        {
            try
            {
                // Desconecta o cliente com problema
                if (gameClientEvent?.Client != null)
                {
                    gameClientEvent.Client.SetGameQuit(true);
                    gameClientEvent.Client.Disconnect();
                }

                // Salva dados de erro para debug
                SaveErrorPacket(gameClientEvent?.Client, data, ex);
            }
            catch (Exception saveEx)
            {
                _logger.Error($"Erro ao salvar dados de erro: {saveEx.Message}");
            }
        }

        /// <summary>
        /// Salva pacotes com erro para análise posterior
        /// </summary>
        private void SaveErrorPacket(GameClient? client, byte[] data, Exception ex)
        {
            try
            {
                if (client == null || data == null) return;

                var fileName = $"PacketErrors/{client.AccountId}_{DateTime.Now:dd_MM_HH_mm_ss}.txt";
                Directory.CreateDirectory("PacketErrors");

                var errorInfo = $"Error: {ex.Message}\nStack: {ex.StackTrace}\nData Length: {data.Length}\n\n";
                var errorBytes = Encoding.UTF8.GetBytes(errorInfo);

                using var fs = File.Create(fileName);
                fs.Write(errorBytes);
                fs.Write(data);

                _logger.Debug($"Pacote de erro salvo: {fileName}");
            }
            catch (Exception saveEx)
            {
                _logger.Error($"Falha ao salvar pacote de erro: {saveEx.Message}");
            }
        }

        #endregion

        #region IHostedService Implementation

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.Information($"Iniciando {GetType().Name}...");
            Console.Title = $"DMO - {GetType().Name}";

            _hostApplicationLifetime.ApplicationStarted.Register(OnStarted);
            _hostApplicationLifetime.ApplicationStopping.Register(OnStopping);
            _hostApplicationLifetime.ApplicationStopped.Register(OnStopped);

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        private void OnStarted()
        {
            var address = _configuration[CharacterServerAddress];
            var port = _configuration[CharacterServerPort];
            var backlog = _configuration[CharacterServerBacklog];

            if (!Listen(address!, port!, backlog))
            {
                _logger.Error("Incapaz de iniciar servidor. Verifique as configurações de ligação.");
                _hostApplicationLifetime.StopApplication();
                return;
            }

            _logger.Information($"{GetType().Name} iniciado em {address}:{port}");
        }

        private void OnStopping()
        {
            _logger.Information($"Parando {GetType().Name}...");
            Shutdown();
        }

        private void OnStopped()
        {
            _cleanupTimer?.Dispose();
            _logger.Information($"{GetType().Name} parado.");
        }

        #endregion
    }
}