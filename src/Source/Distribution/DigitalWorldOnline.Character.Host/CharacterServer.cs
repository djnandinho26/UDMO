using DigitalWorldOnline.Commons.Entities;
using DigitalWorldOnline.Commons.Interfaces;
using DigitalWorldOnline.Commons.Packets.CharacterServer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace DigitalWorldOnline.Character
{
    public sealed class CharacterServer : GameServer, IHostedService
    {
        private readonly IHostApplicationLifetime _hostApplicationLifetime;
        private readonly IConfiguration _configuration;
        private readonly IProcessor _processor;
        private readonly ILogger _logger;

        private const int OnConnectEventHandshakeHandler = 65535;

        public CharacterServer(
            IHostApplicationLifetime hostApplicationLifetime,
            IConfiguration configuration,
            IProcessor processor,
            ILogger logger
            )
        {
            OnConnect += OnConnectEvent;
            OnDisconnect += OnDisconnectEvent;
            DataReceived += OnDataReceivedEvent;

            _hostApplicationLifetime = hostApplicationLifetime;
            _configuration = configuration;
            _processor = processor;
            _logger = logger;
        }

        /// <summary>
        /// Event triggered everytime that a game client connects to the server.
        /// </summary>
        /// <param name="sender">The object itself</param>
        /// <param name="gameClientEvent">Game client who connected</param>
        private void OnConnectEvent(object sender, GameClientEvent gameClientEvent)
        {
            var clientIpAddress = gameClientEvent.Client.ClientAddress.Split(':')?.FirstOrDefault();

            //if (InvalidConnection(clientIpAddress))
            //{
            //    _logger.Information($"Blocked connection event from {gameClientEvent.Client.HiddenAddress}. Blocked Addresses: {RefusedAddresses.Count}");

            //    if (!string.IsNullOrEmpty(clientIpAddress) && !RefusedAddresses.Contains(clientIpAddress))
            //        RefusedAddresses.Add(clientIpAddress);

            //    gameClientEvent.Client.Disconnect();
            //    RemoveClient(gameClientEvent.Client);
            //}
            //else
            //{
            //    _logger.Information($"Accepted connection event from {gameClientEvent.Client.HiddenAddress}.");

            //    gameClientEvent.Client.SetHandshake((short)(DateTimeOffset.UtcNow.ToUnixTimeSeconds() & OnConnectEventHandshakeHandler));

            //    if (gameClientEvent.Client.IsConnected)
            //    {
            //        _logger.Debug($"Sending handshake for request source {gameClientEvent.Client.ClientAddress}.");
            //        gameClientEvent.Client.Send(new OnConnectEventConnectionPacket(gameClientEvent.Client.Handshake));
            //    }
            //    else
            //        _logger.Warning($"Request source {gameClientEvent.Client.ClientAddress} has been disconnected.");
            //}

            _logger.Information($"Accepted connection event from {gameClientEvent.Client.HiddenAddress}.");

            gameClientEvent.Client.SetHandshake((short)(DateTimeOffset.UtcNow.ToUnixTimeSeconds() &
                                                        OnConnectEventHandshakeHandler));

            if (gameClientEvent.Client.IsConnected)
            {
                _logger.Debug($"Sending handshake for request source {gameClientEvent.Client.ClientAddress}.");
                gameClientEvent.Client.Send(new OnConnectEventConnectionPacket(gameClientEvent.Client.Handshake));
            }
            else
                _logger.Warning($"Request source {gameClientEvent.Client.ClientAddress} has been disconnected.");
        }

        /// <summary>
        /// Event triggered everytime the game client disconnects from the server.
        /// </summary>
        /// <param name="sender">The object itself</param>
        /// <param name="gameClientEvent">Game client who disconnected</param>
        private void OnDisconnectEvent(object sender, GameClientEvent gameClientEvent)
        {
            if (!string.IsNullOrEmpty(gameClientEvent.Client.ClientAddress))
            {
                _logger.Information($"Received disconnection event for {gameClientEvent.Client.HiddenAddress}.");
                _logger.Debug(
                    $"Source disconnected: {gameClientEvent.Client.ClientAddress}. Account: {gameClientEvent.Client.AccountId}.");
            }
        }

        /// <summary>
        /// Event triggered everytime the game client sends a TCP packet.
        /// </summary>
        /// <param name="sender">The object itself</param>
        /// <param name="gameClientEvent">Game client who sent the packet</param>
        /// <param name="data">The packet content, in byte array</param>
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
                while (offset < data.Length)
                {
                    // Verificar se temos bytes suficientes para ler o tamanho do pacote
                    if (data.Length - offset < 2)
                    {
                        _logger.Warning($"Pacote incompleto recebido de {gameClientEvent.Client.ClientAddress}. Bytes restantes: {data.Length - offset}");
                        return;
                    }

                    // Ler o tamanho do pacote (primeiros 2 bytes em little-endian)
                    int packetSize = BitConverter.ToUInt16(data, offset);

                    // Se encontrar um pacote com tamanho 0, registra e SAIR completamente do processamento
                    if (packetSize == 0)
                    {
                        //_logger.Warning($"Pacote com tamanho 0 recebido de {gameClientEvent.Client.ClientAddress}. Interrompendo processamento.");
                        return; // Sai imediatamente do método
                    }

                    // Verificar se o tamanho do pacote é válido
                    if (packetSize < 0 || packetSize > 1024 * 16)
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
                    int checksumIndex = offset + (packetSize - 2);
                    bool checksumValid = true;

                    // Verificar se o índice do checksum está dentro dos limites do array
                    if (checksumIndex >= 0 && checksumIndex < data.Length - 1)
                    {
                        try
                        {
                            int packetChecksum = BitConverter.ToUInt16(data, checksumIndex);

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
        /// The default hosted service "starting" method.
        /// </summary>
        /// <param name="cancellationToken">Control token for the operation</param>
        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.Information($"Starting {GetType().Name}...");

            Console.Title = $"DMO - {GetType().Name}";

            _hostApplicationLifetime.ApplicationStarted.Register(OnStarted);
            _hostApplicationLifetime.ApplicationStopping.Register(OnStopping);
            _hostApplicationLifetime.ApplicationStopped.Register(OnStopped);

            return Task.CompletedTask;
        }

        /// <summary>
        /// The default hosted service "stopping" method
        /// </summary>
        /// <param name="cancellationToken">Control token for the operation</param>
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        /// <summary>
        /// The default hosted service "started" method action
        /// </summary>
        private void OnStarted()
        {
            if (!Listen(_configuration[CharacterServerAddress],
                    _configuration[CharacterServerPort],
                    _configuration[CharacterServerBacklog]))
            {
                _logger.Error("Unable to start. Check the binding configurations.");
                _hostApplicationLifetime.StopApplication();
                return;
            }

            _logger.Information($"{GetType().Name} started.");
        }

        /// <summary>
        /// The default hosted service "stopping" method action
        /// </summary>
        private void OnStopping()
        {
            _logger.Information($"Disconnecting clients from {GetType().Name}...");

            Shutdown();
        }

        /// <summary>
        /// The default hosted service "stopped" method action
        /// </summary>
        private void OnStopped()
        {
            _logger.Information($"{GetType().Name} stopped.");
        }
    }
}