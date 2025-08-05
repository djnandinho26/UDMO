using System;
using System.IO;
using System.Collections.Concurrent;

namespace DigitalWorldOnline.Commons.Writers
{
    /// <summary>
    /// Classe responsável por escrever dados para pacotes de rede.
    /// Implementa a serialização e o cálculo de checksum do pacote.
    /// CORREÇÃO: Thread-safe com sessões isoladas por instância.
    /// </summary>
    public class PacketWriter : PacketWriterBase
    {
        /// <summary>
        /// Classe para armazenar dados de uma sessão específica
        /// </summary>
        private class PacketSession
        {
            public byte[]? SerializedBuffer { get; set; }
            public bool IsFinalized { get; set; }
            public MemoryStream Stream { get; set; }
            public int PacketType { get; set; }

            public PacketSession(int packetType)
            {
                PacketType = packetType;
                Stream = new MemoryStream();
                // Reserva espaço para o cabeçalho de comprimento (4 bytes)
                Stream.Write(new byte[4], 0, 4);
                IsFinalized = false;
            }

            public void Dispose()
            {
                Stream?.Dispose();
                SerializedBuffer = null;
            }
        }

        /// <summary>
        /// CORREÇÃO: Sessão atual sendo usada por esta instância (não estática)
        /// </summary>
        private PacketSession? _currentSession;

        /// <summary>
        /// Tipo do pacote atual
        /// </summary>
        private int? _currentPacketType;

        /// <summary>
        /// Flag para indicar se o objeto foi descartado.
        /// </summary>
        private volatile bool _disposed;

        /// <summary>
        /// Lock para operações thread-safe
        /// </summary>
        private readonly object _lock = new object();

        /// <summary>
        /// Obtém o buffer serializado da sessão atual, se já tiver sido gerado.
        /// </summary>
        public byte[]? Buffer
        {
            get
            {
                lock (_lock)
                {
                    return _currentSession?.SerializedBuffer;
                }
            }
        }

        /// <summary>
        /// Indica se o pacote atual foi finalizado e serializado.
        /// </summary>
        public bool IsFinalized
        {
            get
            {
                lock (_lock)
                {
                    return _currentSession?.IsFinalized ?? false;
                }
            }
        }

        /// <summary>
        /// Obtém o tipo do pacote atual
        /// </summary>
        public int? CurrentPacketType
        {
            get
            {
                lock (_lock)
                {
                    return _currentPacketType;
                }
            }
        }

        /// <summary>
        /// Inicializa uma nova instância da classe PacketWriter.
        /// </summary>
        public PacketWriter() : base()
        {
            // Inicialização mínima - a sessão será criada quando Type() for chamado
        }

        /// <summary>
        /// Inicializa uma nova instância da classe PacketWriter com um tipo específico de pacote.
        /// </summary>
        /// <param name="packetType">O tipo de pacote a ser serializado</param>
        public PacketWriter(int packetType) : this()
        {
            Type(packetType);
        }

        /// <summary>
        /// CORREÇÃO: Cria uma nova sessão para esta instância
        /// </summary>
        private PacketSession CreateSession(int packetType)
        {
            return new PacketSession(packetType);
        }

        /// <summary>
        /// CORREÇÃO: Define a sessão atual baseada no tipo de pacote (não usa cache estático)
        /// </summary>
        private void SetCurrentSession(int packetType)
        {
            lock (_lock)
            {
                // Limpa sessão anterior se existir
                _currentSession?.Dispose();

                _currentPacketType = packetType;
                _currentSession = CreateSession(packetType);

                // Atualiza a referência do Packet para a sessão atual
                Packet = _currentSession.Stream;
            }
        }

        /// <summary>
        /// Serializa o pacote da sessão atual, calculando seu comprimento total e checksum.
        /// CORREÇÃO: Utiliza MemoryStream expansível para evitar o erro de stream não expansível.
        /// </summary>
        /// <returns>Um array de bytes representando o pacote completo</returns>
        /// <exception cref="ObjectDisposedException">Lançada quando o objeto já foi descartado</exception>
        /// <exception cref="InvalidOperationException">Lançada quando nenhum tipo de pacote foi definido</exception>
        public byte[] Serialize()
        {
            lock (_lock)
            {
                ThrowIfDisposed();

                if (_currentSession == null)
                    throw new InvalidOperationException("Nenhum tipo de pacote foi definido. Chame Type() primeiro.");

                // Se já existe um buffer em cache e a sessão não foi modificada, retorna o cache
                if (_currentSession.SerializedBuffer != null && _currentSession.IsFinalized)
                    return _currentSession.SerializedBuffer;

                try
                {
                    var workingStream = _currentSession.Stream;

                    // Obtém dados atuais do stream
                    byte[] currentData = workingStream.ToArray();

                    // CORREÇÃO: Cria um MemoryStream expansível em vez de usar array fixo
                    using var tempStream = new MemoryStream();

                    // Escreve os dados atuais no stream temporário
                    tempStream.Write(currentData, 0, currentData.Length);

                    // Adiciona o checksum placeholder (4 bytes) - agora o stream pode expandir
                    tempStream.Write(BitConverter.GetBytes(0), 0, 4);

                    // Obter o buffer do MemoryStream temporário
                    byte[] buffer = tempStream.ToArray();
                    int bufferLength = buffer.Length;

                    // Calcula e escreve o comprimento no início do buffer
                    Span<byte> lengthBytes = BitConverter.GetBytes(bufferLength);
                    lengthBytes.CopyTo(buffer.AsSpan(0, 4));

                    // Calcula e escreve o checksum no final do buffer
                    int checksum = bufferLength ^ CheckSumValidation;
                    Span<byte> checksumBytes = BitConverter.GetBytes(checksum);
                    checksumBytes.CopyTo(buffer.AsSpan(bufferLength - 4, 4));

                    // Cache o buffer mas NÃO marca como finalizado para permitir mais escritas
                    _currentSession.SerializedBuffer = buffer;

                    return buffer;
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException("Erro durante a serialização do pacote", ex);
                }
            }
        }

        /// <summary>
        /// Limpa o buffer serializado da sessão atual, permitindo modificações adicionais.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Lançada quando o objeto já foi descartado</exception>
        public void ResetBuffer()
        {
            lock (_lock)
            {
                ThrowIfDisposed();

                if (_currentSession == null)
                    return;

                _currentSession.SerializedBuffer = null;
                _currentSession.IsFinalized = false;

                // Recria o stream da sessão
                var packetType = _currentSession.PacketType;
                _currentSession.Stream?.Dispose();
                _currentSession.Stream = new MemoryStream();
                _currentSession.Stream.Write(new byte[4], 0, 4); // Reescreve o header

                // Reescreve o tipo do pacote
                _currentSession.Stream.Write(BitConverter.GetBytes(packetType), 0, 2);

                // Atualiza a referência
                Packet = _currentSession.Stream;
            }
        }

        /// <summary>
        /// Valida se o pacote pode ser modificado.
        /// </summary>
        private void ThrowIfFinalized()
        {
            if (_currentSession?.IsFinalized == true)
            {
                // Auto-reset para tornar a classe mais defensiva
                ResetBuffer();
            }
        }

        /// <summary>
        /// Valida se o objeto não foi descartado.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Lançada quando o objeto já foi descartado</exception>
        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(PacketWriter));
        }

        /// <summary>
        /// Valida se existe uma sessão atual ativa
        /// </summary>
        private void ThrowIfNoSession()
        {
            if (_currentSession == null)
                throw new InvalidOperationException("Nenhuma sessão ativa. Chame Type() primeiro para definir o tipo do pacote.");
        }

        /// <summary>
        /// CORREÇÃO: Define o tipo do pacote e cria uma nova sessão para esta instância.
        /// </summary>
        public override void Type(int type)
        {
            lock (_lock)
            {
                ThrowIfDisposed();

                // Define a sessão atual para este tipo
                SetCurrentSession(type);

                // Escreve o tipo no stream da sessão (substitui o comportamento da classe base)
                _currentSession.Stream.Seek(4, SeekOrigin.Begin); // Posiciona após o header
                _currentSession.Stream.Write(BitConverter.GetBytes((ushort)type), 0, 2);
            }
        }

        /// <summary>
        /// CORREÇÃO: Método helper para operações de escrita thread-safe simplificado
        /// </summary>
        private void SafeWrite(Action writeAction)
        {
            lock (_lock)
            {
                ThrowIfDisposed();
                ThrowIfNoSession();
                ThrowIfFinalized();

                // Limpa buffer em cache para forçar nova serialização
                if (_currentSession != null)
                {
                    _currentSession.SerializedBuffer = null;
                }

                writeAction();
            }
        }

        /// <summary>
        /// Sobrescreve WriteInt para validar estado antes da escrita.
        /// </summary>
        public new void WriteInt(int value)
        {
            SafeWrite(() => base.WriteInt(value));
        }

        /// <summary>
        /// Sobrescreve WriteString para validar estado antes da escrita.
        /// </summary>
        public new void WriteString(string value)
        {
            SafeWrite(() => base.WriteString(value));
        }

        /// <summary>
        /// Sobrescreve WriteBytes para validar estado antes da escrita.
        /// </summary>
        public new void WriteBytes(byte[] buffer)
        {
            SafeWrite(() => base.WriteBytes(buffer));
        }

        /// <summary>
        /// Sobrescreve WriteByte para validar estado antes da escrita.
        /// </summary>
        public new void WriteByte(byte value)
        {
            SafeWrite(() => base.WriteByte(value));
        }

        /// <summary>
        /// CORREÇÃO: WriteShort com logging para debug
        /// </summary>
        public new void WriteShort(short value)
        {
            SafeWrite(() => base.WriteShort(value));
        }

        /// <summary>
        /// Sobrescreve WriteInt64 para validar estado antes da escrita.
        /// </summary>
        public new void WriteInt64(long value)
        {
            SafeWrite(() => base.WriteInt64(value));
        }

        /// <summary>
        /// Sobrescreve WriteUShort para validar estado antes da escrita.
        /// </summary>
        public new void WriteUShort(ushort value)
        {
            SafeWrite(() => base.WriteUShort(value));
        }

        /// <summary>
        /// Sobrescreve WriteUInt para validar estado antes da escrita.
        /// </summary>
        public new void WriteUInt(uint value)
        {
            SafeWrite(() => base.WriteUInt(value));
        }

        /// <summary>
        /// Sobrescreve WriteUInt64 para validar estado antes da escrita.
        /// </summary>
        public new void WriteUInt64(ulong value)
        {
            SafeWrite(() => base.WriteUInt64(value));
        }

        /// <summary>
        /// Sobrescreve WriteFloat para validar estado antes da escrita.
        /// </summary>
        public new void WriteFloat(float value)
        {
            SafeWrite(() => base.WriteFloat(value));
        }

        /// <summary>
        /// Obtém informações sobre o pacote atual para depuração.
        /// </summary>
        /// <returns>Informações detalhadas sobre o pacote</returns>
        public string GetPacketInfo()
        {
            lock (_lock)
            {
                var currentType = _currentSession?.PacketType.ToString() ?? "None";
                var length = _currentSession?.Stream?.Length ?? 0;
                var isFinalized = _currentSession?.IsFinalized ?? false;
                var hasBuffer = _currentSession?.SerializedBuffer != null;

                return $"PacketWriter [Type: {currentType}, Length: {length}, IsFinalized: {isFinalized}, BufferCached: {hasBuffer}, Disposed: {_disposed}]";
            }
        }

        /// <summary>
        /// Obtém o buffer de bytes serializado em formato de string legível.
        /// </summary>
        /// <returns>Representação string do pacote serializado</returns>
        public override string ToString()
        {
            lock (_lock)
            {
                if (_disposed)
                    return "PacketWriter [Disposed]";

                if (_currentSession == null)
                    return "PacketWriter [No Active Session]";

                try
                {
                    byte[] data = Serialize();
                    return $"Pacote Type:{_currentSession.PacketType} [{data.Length} bytes]: {BitConverter.ToString(data).Replace("-", " ")}";
                }
                catch
                {
                    return $"Pacote [não serializado]: {GetPacketInfo()}";
                }
            }
        }

        /// <summary>
        /// CORREÇÃO: Implementação do Dispose que limpa recursos específicos desta classe.
        /// </summary>
        public new void Dispose()
        {
            lock (_lock)
            {
                if (_disposed)
                    return;

                try
                {
                    // Limpa sessão atual
                    _currentSession?.Dispose();
                    _currentSession = null;
                    _currentPacketType = null;

                    // Marca como descartado
                    _disposed = true;

                    // Chama o Dispose da classe base
                    base.Dispose();
                }
                catch
                {
                    // Ignora erros durante o dispose
                }
                finally
                {
                    GC.SuppressFinalize(this);
                }
            }
        }

        /// <summary>
        /// Finalizer para garantir limpeza de recursos.
        /// </summary>
        ~PacketWriter()
        {
            Dispose();
        }
    }
}