using System.Text;

namespace DigitalWorldOnline.Commons.Writers
{
    public abstract class PacketWriterBase : IDisposable
    {
        public MemoryStream Packet { get; protected set; }
        public int Length => (int)Packet.Length;

        public const int CheckSumValidation = 0x2B4D1A3C;

        /// <summary>
        /// Flag para controlar se o objeto foi descartado
        /// </summary>
        private bool _disposed = false;

        public PacketWriterBase()
        {
            Packet = new MemoryStream();
        }

        #region [Position]
        public virtual void Seek(long position)
        {
            ThrowIfDisposed();
            Packet.Seek(position, SeekOrigin.Begin);
        }

        public virtual void Skip(long bytes)
        {
            ThrowIfDisposed();
            Packet.Seek(bytes, SeekOrigin.Current);
        }
        #endregion

        #region [Write Data]
        public virtual void Type(int type)
        {
            ThrowIfDisposed();
            // CORREÇÃO: Escreve apenas 2 bytes do tipo, não 4
            byte[] typeBytes = BitConverter.GetBytes((ushort)type);
            Packet.Write(typeBytes, 0, 2);
        }

        public void WriteByte(byte value)
        {
            ThrowIfDisposed();
            // CORREÇÃO: Escreve apenas 1 byte diretamente
            Packet.WriteByte(value);
        }

        public void WriteBytes(byte[] buffer)
        {
            ThrowIfDisposed();
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));

            Packet.Write(buffer, 0, buffer.Length);
        }

        public void WriteShort(short value)
        {
            ThrowIfDisposed();
            byte[] bytes = BitConverter.GetBytes(value);
            Packet.Write(bytes, 0, 2);
        }

        public void WriteUShort(ushort value)
        {
            ThrowIfDisposed();
            byte[] bytes = BitConverter.GetBytes(value);
            Packet.Write(bytes, 0, 2);
        }

        public void WriteInt(int value)
        {
            ThrowIfDisposed();
            byte[] bytes = BitConverter.GetBytes(value);
            Packet.Write(bytes, 0, 4);
        }

        public void WriteInt(int value, int pos)
        {
            ThrowIfDisposed();
            long currentPos = Packet.Position;
            Packet.Seek(pos, SeekOrigin.Begin);
            byte[] bytes = BitConverter.GetBytes(value);
            Packet.Write(bytes, 0, 4);
            Packet.Position = currentPos; // CORREÇÃO: Restaura posição original
        }

        public void WriteUInt(uint value)
        {
            ThrowIfDisposed();
            byte[] bytes = BitConverter.GetBytes(value);
            Packet.Write(bytes, 0, 4);
        }

        public void WriteUInt(uint value, int pos)
        {
            ThrowIfDisposed();
            long currentPos = Packet.Position;
            Packet.Seek(pos, SeekOrigin.Begin);
            byte[] bytes = BitConverter.GetBytes(value);
            Packet.Write(bytes, 0, 4);
            Packet.Position = currentPos; // CORREÇÃO: Restaura posição original
        }

        public void WriteInt64(long value)
        {
            ThrowIfDisposed();
            byte[] bytes = BitConverter.GetBytes(value);
            Packet.Write(bytes, 0, 8);
        }

        public void WriteUInt64(ulong value)
        {
            ThrowIfDisposed();
            byte[] bytes = BitConverter.GetBytes(value);
            Packet.Write(bytes, 0, 8);
        }

        /// <summary>
        /// CORREÇÃO: WriteString agora escreve 1 byte por caractere usando ASCII
        /// </summary>
        public void WriteString(string value)
        {
            ThrowIfDisposed();
            value ??= string.Empty; // Sintaxe moderna do C# 13

            // Converte para ASCII (1 byte por caractere)
            byte[] buffer = Encoding.ASCII.GetBytes(value);

            // Escreve o comprimento da string em bytes
            WriteShort((short)buffer.Length);

            // Escreve os bytes da string (1 byte por caractere)
            Packet.Write(buffer, 0, buffer.Length);
        }

        /// <summary>
        /// CORREÇÃO: WriteString com posição - 1 byte por caractere usando ASCII
        /// </summary>
        public void WriteString(string value, int pos)
        {
            ThrowIfDisposed();
            long currentPos = Packet.Position;
            Packet.Seek(pos, SeekOrigin.Begin);

            value ??= string.Empty;

            // Converte para ASCII (1 byte por caractere)
            byte[] buffer = Encoding.ASCII.GetBytes(value);

            // Escreve o comprimento da string em bytes
            WriteShort((short)buffer.Length);

            // Escreve os bytes da string (1 byte por caractere)
            Packet.Write(buffer, 0, buffer.Length);

            Packet.Position = currentPos; // CORREÇÃO: Restaura posição original
        }

        /// <summary>
        /// CORREÇÃO: WriteZString corrigido - escreve caracteres wide (2 bytes cada)
        /// </summary>
        public void WriteZString(string value)
        {
            ThrowIfDisposed();
            value ??= string.Empty;

            // Escreve o comprimento em caracteres (não em bytes)
            WriteShort((short)value.Length);

            // Escreve cada caractere como wchar_t (2 bytes)
            foreach (char c in value)
            {
                WriteShort((short)c);
            }
        }

        /// <summary>
        /// CORREÇÃO: WriteZString com posição - caracteres wide (2 bytes cada)
        /// </summary>
        public void WriteZString(string value, int pos)
        {
            ThrowIfDisposed();
            long currentPos = Packet.Position;
            Packet.Seek(pos, SeekOrigin.Begin);

            value ??= string.Empty;

            // Escreve o comprimento em caracteres (não em bytes)
            WriteShort((short)value.Length);

            // Escreve cada caractere como wchar_t (2 bytes)
            foreach (char c in value)
            {
                WriteShort((short)c);
            }

            Packet.Position = currentPos; // CORREÇÃO: Restaura posição original
        }

        public void WriteFloat(float value)
        {
            ThrowIfDisposed();
            byte[] bytes = BitConverter.GetBytes(value);
            Packet.Write(bytes, 0, 4);
        }

        #endregion

        /// <summary>
        /// Verifica se o objeto foi descartado e lança exceção se necessário
        /// </summary>
        protected void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(PacketWriterBase));
        }

        /// <summary>
        /// CORREÇÃO: Implementação robusta do padrão Dispose
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Implementação protegida do padrão Dispose
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Libera recursos gerenciados
                    Packet?.Close();
                    Packet?.Dispose();
                }

                _disposed = true;
            }
        }

        /// <summary>
        /// Finalizer para garantir limpeza de recursos
        /// </summary>
        ~PacketWriterBase()
        {
            Dispose(false);
        }
    }
}