using DigitalWorldOnline.Commons.Entities;
using DigitalWorldOnline.Commons.Interfaces;
using DigitalWorldOnline.Commons.Packets.AuthenticationServer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Collections.Concurrent;

namespace DigitalWorldOnline.Account
{
    public sealed class AuthenticationServer : GameServer, IHostedService
    {
        private readonly IHostApplicationLifetime _hostApplicationLifetime;
        private readonly IConfiguration _configuration;
        private readonly IProcessor _processor;
        private readonly ILogger _logger;

        // Constante para o limite máximo de conexões simultâneas
        private const int MaxConcurrentConnections = 300;

        // Dicionário para armazenar os últimos tempos de conexão de cada IP
        private readonly ConcurrentDictionary<string, DateTime> _lastConnectionAttempts;

        // Lista de IPs bloqueados temporariamente
        private readonly ConcurrentDictionary<string, DateTime> _temporarilyBlockedIps;

        // Tempo mínimo entre conexões (em segundos)
        private const int MinTimeBetweenConnections = 1;

        // Tempo de duração do bloqueio (em minutos)
        private const int BlockDurationMinutes = 20;

        private const int OnConnectEventHandshakeHandler = 65535;

        public AuthenticationServer(IHostApplicationLifetime hostApplicationLifetime,
            IConfiguration configuration,
            IProcessor processor,
            ILogger logger)
        {
            OnConnect += OnConnectEvent;
            OnDisconnect += OnDisconnectEvent;
            DataReceived += OnDataReceivedEvent;

            _hostApplicationLifetime = hostApplicationLifetime;
            _configuration = configuration;
            _processor = processor;
            _logger = logger;

            // Inicializa as coleções concorrentes
            _lastConnectionAttempts = new ConcurrentDictionary<string, DateTime>();
            _temporarilyBlockedIps = new ConcurrentDictionary<string, DateTime>();

            // Inicia um timer para limpar IPs bloqueados expirados
            StartCleanupTimer();
        }

        /// <summary>
        /// Inicia um timer para limpar periodicamente os IPs bloqueados que já expiraram
        /// </summary>
        private void StartCleanupTimer()
        {
            Timer cleanupTimer = new Timer(CleanupBlockedIps, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
        }

        /// <summary>
        /// Remove IPs bloqueados que já passaram do tempo de bloqueio
        /// </summary>
        private void CleanupBlockedIps(object state)
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
                    _logger.Information($"Desbloqueando IP {ip} após período de bloqueio.");
                }
            }
        }

        /// <summary>
        /// Verifica se um IP está bloqueado temporariamente
        /// </summary>
        private bool IsIpBlocked(string ipAddress)
        {
            // Se o IP estiver no dicionário de bloqueados e o tempo de bloqueio não expirou
            if (_temporarilyBlockedIps.TryGetValue(ipAddress, out DateTime blockUntil))
            {
                if (DateTime.Now < blockUntil)
                {
                    return true;
                }
                // Se o bloqueio expirou, remove da lista
                _temporarilyBlockedIps.TryRemove(ipAddress, out _);
            }

            return false;
        }

        /// <summary>
        /// Verifica se uma nova conexão deve ser bloqueada com base na frequência de conexões
        /// </summary>
        private bool ShouldBlockConnection(string ipAddress)
        {
            var now = DateTime.Now;

            // Se o IP já está bloqueado, rejeita a conexão
            if (IsIpBlocked(ipAddress))
            {
                return true;
            }

            // Verifica se o IP fez uma conexão recentemente
            if (_lastConnectionAttempts.TryGetValue(ipAddress, out DateTime lastAttempt))
            {
                TimeSpan timeSinceLastAttempt = now - lastAttempt;

                // Se a última tentativa foi há menos de MinTimeBetweenConnections segundos
                if (timeSinceLastAttempt.TotalSeconds < MinTimeBetweenConnections)
                {
                    // Bloqueia o IP por BlockDurationMinutes minutos
                    _temporarilyBlockedIps[ipAddress] = now.AddMinutes(BlockDurationMinutes);
                    _logger.Warning($"IP {ipAddress} bloqueado por {BlockDurationMinutes} minutos por tentar reconectar em menos de {MinTimeBetweenConnections} segundos.");
                    return true;
                }
            }

            // Atualiza o timestamp da última conexão para este IP
            _lastConnectionAttempts[ipAddress] = now;
            return false;
        }

        /// <summary>
        /// Sobrescreve o método OnClientConnection para controlar o número de conexões e bloquear IPs muito frequentes
        /// </summary>
        public override void OnClientConnection(GameClientEvent e)
        {
            // Extrai o IP do cliente sem a porta
            string clientIp = e.Client.ClientAddress?.Split(':').FirstOrDefault() ?? "Desconhecido";

            // Verifica se essa conexão deve ser bloqueada
            if (ShouldBlockConnection(clientIp))
            {
                _logger.Warning($"Conexão rejeitada de {clientIp}: IP tentando reconectar muito rapidamente ou está bloqueado.");

                try
                {
                    // Envia mensagem e fecha conexão
                    e.Client.Socket.Send(Encoding.UTF8.GetBytes("Conexão rejeitada: muitas tentativas em um curto período. Tente novamente mais tarde.\n"));
                    e.Client.Socket.Shutdown(SocketShutdown.Both);
                    e.Client.Socket.Close();
                }
                catch (Exception ex)
                {
                    _logger.Error($"Erro ao fechar conexão rejeitada: {ex.Message}");
                }

                // Remove o cliente da lista
                RemoveClient(e.Client, false);
                return;
            }

            // Verifica se podemos aceitar mais uma conexão (limite máximo)
            if (Clients.Count > MaxConcurrentConnections)
            {
                // Se excedeu o limite, rejeita a conexão
                _logger.Warning($"Conexão rejeitada de {clientIp}: limite de {MaxConcurrentConnections} conexões atingido.");

                try
                {
                    // Envia mensagem e fecha conexão
                    e.Client.Socket.Send(Encoding.UTF8.GetBytes("Servidor cheio. Tente novamente mais tarde.\n"));
                    e.Client.Socket.Shutdown(SocketShutdown.Both);
                    e.Client.Socket.Close();
                }
                catch (Exception ex)
                {
                    _logger.Error($"Erro ao fechar conexão rejeitada: {ex.Message}");
                }

                // Remove o cliente da lista
                RemoveClient(e.Client, false);
                return;
            }

            // Se a conexão for aceita, registra no log
            _logger.Debug($"Conexão aceita de {clientIp}. Conexões ativas: {Clients.Count}/{MaxConcurrentConnections}");

            // Chama o método da classe base para processar a conexão
            base.OnClientConnection(e);
        }

        /// <summary>
        /// Evento desencadeado sempre que um cliente de jogo se conecta ao servidor.
        /// </summary>
        /// <param name="sender">O próprio objeto</param>
        /// <param name="gameClientEvent">Cliente de jogo que se conectou</param>
        private void OnConnectEvent(object sender, GameClientEvent gameClientEvent)
        {
            var clientIpAddress = gameClientEvent.Client.ClientAddress?.Split(':')?.FirstOrDefault();

            _logger.Information($"Evento de conexão aceito de {gameClientEvent.Client.HiddenAddress}.");

            gameClientEvent.Client.SetHandshake((short)(DateTimeOffset.Now.ToUnixTimeSeconds() & OnConnectEventHandshakeHandler));

            if (gameClientEvent.Client.IsConnected)
            {
                _logger.Debug($"Enviando handshake para fonte de solicitação {gameClientEvent.Client.ClientAddress}.");
                gameClientEvent.Client.Send(new OnConnectEventConnectionPacket(gameClientEvent.Client.Handshake));
                //gameClientEvent.Client.Send(new InjectPacket(65535, "9188B1D02D0E11D168F100"));
            }
            else
                _logger.Warning($"Fonte de solicitação {gameClientEvent.Client.ClientAddress} foi desconectado.");
        }

        /// <summary>
        /// Evento acionado sempre que o cliente do jogo se desconecta do servidor.
        /// </summary>
        /// <param name="sender">O próprio objeto</param>
        /// <param name="gameClientEvent">Cliente de jogo que desconectou</param>
        private void OnDisconnectEvent(object sender, GameClientEvent gameClientEvent)
        {
            if (!string.IsNullOrEmpty(gameClientEvent.Client.ClientAddress))
            {
                _logger.Information($"Recebido evento de desconexão para {gameClientEvent.Client.HiddenAddress}.");
                _logger.Debug($"Fonte desconectada: {gameClientEvent.Client.ClientAddress}. Account: {gameClientEvent.Client.AccountId}.");
            }
        }

        /// <summary>
        /// Evento acionado sempre que o cliente do jogo envia um pacote TCP.
        /// </summary>
        /// <param name="sender">O próprio objeto</param>
        /// <param name="gameClientEvent">Cliente de jogo que enviou o pacote</param>
        /// <param name="data">O conteúdo do pacote, em matriz de bytes</param>
        private void OnDataReceivedEvent(object sender, GameClientEvent gameClientEvent, byte[] data)
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
                            if (packetChecksum == 0)
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
        /// O método de "partida" de serviço hospedado padrão.
        /// </summary>
        /// <param name="cancellationToken">Token de controle para a operação</param>
        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.Information($"Iniciando {GetType().Name}...");

            Console.Title = $"DMO - {GetType().Name}";

            _hostApplicationLifetime.ApplicationStarted.Register(OnStarted);
            _hostApplicationLifetime.ApplicationStopping.Register(OnStopping);
            _hostApplicationLifetime.ApplicationStopped.Register(OnStopped);

            return Task.CompletedTask;
        }

        /// <summary>
        /// O método de "parada" de serviço hospedado padrão
        /// </summary>
        /// <param name="cancellationToken">Token de controle para a operação</param>
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        /// <summary>
        /// O serviço padrão hospedado "iniciou" ação do método
        /// </summary>
        private void OnStarted()
        {
            string address = _configuration[AuthenticationServerAddress] ?? "0.0.0.0";
            string port = _configuration[AuthenticationServerPort] ?? "7029";
            string backlog = _configuration[AuthenticationServerBacklog] ?? "10";

            if (!Listen(address, port, backlog))
            {
                _logger.Error("Incapaz de começar. Verifique as configurações de ligação.");
                _hostApplicationLifetime.StopApplication();
                return;
            }

            _logger.Information($"{GetType().Name} iniciado Ip: {address} porta {port}.");

            //_logger.Information($"{GetType().Name} iniciado com limite de {MaxConcurrentConnections} conexões simultâneas na porta {port}.");
            //_logger.Information($"IPs que se conectarem em menos de {MinTimeBetweenConnections} segundos serão bloqueados por {BlockDurationMinutes} minutos.");
        }

        /// <summary>
        /// O serviço padrão hospedou o serviço "Stopping" Method Action
        /// </summary>
        private void OnStopping()
        {
            _logger.Information($"Desconectando clientes...");

            Shutdown();
        }

        /// <summary>
        /// O serviço padrão hospedado "parou" ação do método
        /// </summary>
        private void OnStopped()
        {
            _logger.Information($"{GetType().Name} parou.");
        }
    }
}