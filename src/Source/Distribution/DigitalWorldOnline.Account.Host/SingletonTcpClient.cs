using System.Net.Sockets;
using System.Text;

namespace DigitalWorldOnline.Account
{
            /// <summary>
    /// Cliente TCP Singleton para conexão com o servidor.
    /// Implementa o padrão Singleton para garantir uma única instância do cliente TCP em toda a aplicação.
    /// </summary>
    public class SingletonTcpClient : IDisposable
    {
        // Instância singleton e objeto de bloqueio para thread safety
        private static SingletonTcpClient? _instance;
        private static readonly object _lock = new();

        // Propriedades de conexão
        private TcpClient? _tcpClient;
        private StreamReader? _reader;
        private StreamWriter? _writer;
        private bool _disposed;

        // Informações do servidor
        private readonly string _serverIp;
        private readonly int _serverPort;
        private readonly int _connectionTimeout;

        /// <summary>
        /// Construtor privado (padrão Singleton)
        /// </summary>
        /// <param name="serverIp">Endereço IP do servidor</param>
        /// <param name="serverPort">Porta do servidor</param>
        /// <param name="connectionTimeout">Tempo limite de conexão em milissegundos (padrão: 5000ms)</param>
        private SingletonTcpClient(string serverIp, int serverPort, int connectionTimeout = 5000)
        {
            _serverIp = serverIp ?? throw new ArgumentNullException(nameof(serverIp));
            
            if (serverPort <= 0 || serverPort > 65535)
                throw new ArgumentOutOfRangeException(nameof(serverPort), "A porta deve estar entre 1 e 65535");
                
            _serverPort = serverPort;
            _connectionTimeout = connectionTimeout;
        }

        /// <summary>
        /// Obtém a instância singleton do cliente TCP.
        /// </summary>
        /// <param name="serverIp">Endereço IP do servidor</param>
        /// <param name="serverPort">Porta do servidor</param>
        /// <param name="connectionTimeout">Tempo limite de conexão em milissegundos (padrão: 5000ms)</param>
        /// <returns>Instância singleton do cliente TCP</returns>
        public static SingletonTcpClient GetInstance(string serverIp, int serverPort, int connectionTimeout = 5000)
        {
            // Garantir thread safety usando lock
            lock (_lock)
            {
                _instance ??= new SingletonTcpClient(serverIp, serverPort, connectionTimeout);
                return _instance;
            }
        }

        /// <summary>
        /// Verifica se o cliente está conectado ao servidor.
        /// </summary>
        public bool IsConnected => _tcpClient?.Connected == true;

        /// <summary>
        /// Estabelece uma conexão com o servidor se ainda não estiver conectado.
        /// </summary>
        /// <exception cref="SocketException">Lançada quando ocorre um erro de socket durante a conexão.</exception>
        /// <exception cref="TimeoutException">Lançada quando a conexão excede o tempo limite.</exception>
        public async Task ConnectAsync()
        {
            // Verifica se já está conectado
            if (IsConnected)
                return;

            try
            {
                // Cria um novo cliente TCP e configura o timeout
                _tcpClient = new TcpClient();
                
                // Conecta-se ao servidor com timeout
                var connectTask = _tcpClient.ConnectAsync(_serverIp, _serverPort);
                var timeoutTask = Task.Delay(_connectionTimeout);
                
                var completedTask = await Task.WhenAny(connectTask, timeoutTask);
                
                if (completedTask == timeoutTask)
                {
                    _tcpClient.Dispose();
                    _tcpClient = null;
                    throw new TimeoutException($"Tempo limite de conexão excedido ({_connectionTimeout}ms)");
                }

                await connectTask; // Propaga exceções se houver

                // Configura os streams de leitura e escrita
                var stream = _tcpClient.GetStream();
                _reader = new StreamReader(stream, Encoding.UTF8);
                _writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

                Console.WriteLine($"Conectado ao servidor em {_serverIp}:{_serverPort}");
            }
            catch (SocketException ex)
            {
                _tcpClient?.Dispose();
                _tcpClient = null;
                throw new SocketException(ex.ErrorCode);
            }
            catch (Exception ex) when (!(ex is TimeoutException))
            {
                _tcpClient?.Dispose();
                _tcpClient = null;
                throw new InvalidOperationException($"Falha ao conectar ao servidor: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Envia uma mensagem para o servidor.
        /// </summary>
        /// <param name="message">Mensagem a ser enviada</param>
        /// <exception cref="InvalidOperationException">Lançada quando não está conectado ao servidor.</exception>
        public async Task SendMessageAsync(string message)
        {
            if (_writer == null || !IsConnected)
                throw new InvalidOperationException("Não conectado ao servidor.");

            try
            {
                await _writer.WriteLineAsync(message);
            }
            catch (Exception ex)
            {
                throw new IOException($"Erro ao enviar mensagem: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Lê uma resposta do servidor.
        /// </summary>
        /// <returns>A resposta do servidor ou null se não houver resposta</returns>
        /// <exception cref="InvalidOperationException">Lançada quando não está conectado ao servidor.</exception>
        public async Task<string?> ReadResponseAsync()
        {
            if (_reader == null || !IsConnected)
                throw new InvalidOperationException("Não conectado ao servidor.");

            try
            {
                return await _reader.ReadLineAsync();
            }
            catch (Exception ex)
            {
                throw new IOException($"Erro ao ler resposta: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Desconecta do servidor.
        /// </summary>
        public void Disconnect()
        {
            try
            {
                _reader?.Dispose();
                _writer?.Dispose();
                _tcpClient?.Close();
                _tcpClient = null;

                Console.WriteLine("Desconectado do servidor.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao desconectar: {ex.Message}");
            }
        }

        /// <summary>
        /// Limpa recursos gerenciados e não gerenciados.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Método protegido para implementação do padrão Dispose.
        /// </summary>
        /// <param name="disposing">Indica se deve liberar recursos gerenciados</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                Disconnect();
            }

            _disposed = true;
        }

        /// <summary>
        /// Finalizador para garantir a liberação de recursos.
        /// </summary>
        ~SingletonTcpClient()
        {
            Dispose(false);
        }
    }
}