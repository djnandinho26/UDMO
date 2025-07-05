using DigitalWorldOnline.Commons.Interfaces;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DigitalWorldOnline.Commons.Packet;

/// <summary>
/// Classe de utilitários para logging no console e em arquivos.
/// </summary>
public static class SysCons
{
    // Configurações de logging
    private static readonly string LogDirectory = "logs";
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _fileLocks = new ConcurrentDictionary<string, SemaphoreSlim>();

    // Buffer de log para reduzir operações de I/O
    private static readonly ConcurrentDictionary<string, ConcurrentQueue<string>> _logBuffers = new ConcurrentDictionary<string, ConcurrentQueue<string>>();
    private static readonly int _bufferSize = 50;
    private static readonly Timer _flushTimer;

    // Timer para flush periódico dos logs (a cada 2 segundos)
    static SysCons()
    {
        // Garante que o diretório de logs existe
        Directory.CreateDirectory(LogDirectory);

        // Configura o timer para flush periódico
        _flushTimer = new Timer(FlushAllLogs, null, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2));
    }

    #region Console Logging Methods

    public static void LogInfo(string text, params object[] args)
    {
        WriteToConsole("[INFO]", text, args, ConsoleColor.Cyan, ConsoleColor.Gray);
    }

    public static void Loggin(string text, params object[] args)
    {
        WriteToConsole("[LOGIN]", text, args, ConsoleColor.Cyan, ConsoleColor.Magenta);
    }

    public static void LogWarn(string text, params object[] args)
    {
        WriteToConsole("[WARN]", text, args, ConsoleColor.Yellow, ConsoleColor.Gray);
    }

    public static void LogIlegal(string text, params object[] args)
    {
        WriteToConsole("[HACK]", text, args, ConsoleColor.Green, ConsoleColor.Red);
    }

    public static void LogDB(string section, string text, params object[] args)
    {
        WriteToConsole($"[{section}]", text, args, ConsoleColor.Cyan, ConsoleColor.Gray);
    }

    public static void LogError(string text, params object[] args)
    {
        WriteToConsole("[ERROR]", text, args, ConsoleColor.Red, ConsoleColor.Gray);
    }

    public static void Chat(string text, params object[] args)
    {
        WriteToConsole("[CHAT]", text, args, ConsoleColor.DarkRed, ConsoleColor.Gray);
    }

    public static void LogPacket(string text, params object[] args)
    {
        WriteToConsole("[PacketNull]", text, args, ConsoleColor.Red, ConsoleColor.Blue);
    }

    #endregion

    #region File Logging Methods

    public static void LogPacketRecv(string text, params object[] args)
    {
        string logFilename = Path.Combine(LogDirectory, "LogPacketClient.txt");
        WriteToFileBuffered(logFilename, "[PacketRecv]", text, args);
    }

    public static void LogPacketSend(string text, params object[] args)
    {
        string logFilename = Path.Combine(LogDirectory, "LogPacketClient.txt");
        WriteToFileBuffered(logFilename, "[PacketSend]", text, args);
    }

    public static void LogPacketInvalid(string text, params object[] args)
    {
        string logFilename = Path.Combine(LogDirectory, "LogPacketClient.txt");
        WriteToFileBuffered(logFilename, "[PacketInvalido]", text, args);
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Escreve uma mensagem no console com cores formatadas.
    /// </summary>
    private static void WriteToConsole(string prefix, string text, object[] args,
        ConsoleColor prefixColor, ConsoleColor textColor)
    {
        try
        {
            string formattedText = args.Length == 0 ? text : string.Format(text, args);
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");

            // Usar lock para evitar que diferentes threads misturem saídas do console
            lock (Console.Out)
            {
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write($"[{timestamp}] ");
                Console.ForegroundColor = prefixColor;
                Console.Write($"{prefix} ");
                Console.ForegroundColor = textColor;
                Console.WriteLine(formattedText);
                Console.ResetColor();
            }
        }
        catch (Exception ex)
        {
            // Fallback para caso de erro
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[ERRO NO LOG] {ex.Message}");
            Console.ResetColor();
        }
    }

    /// <summary>
    /// Adiciona uma entrada ao buffer de logs.
    /// </summary>
    private static void WriteToFileBuffered(string logFilename, string prefix, string text, object[] args)
    {
        try
        {
            string formattedText = args.Length == 0 ? text : string.Format(text, args);
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            string logLine = $"[{timestamp}] {prefix} {formattedText}";

            // Adiciona ao buffer
            var buffer = _logBuffers.GetOrAdd(logFilename, _ => new ConcurrentQueue<string>());
            buffer.Enqueue(logLine);

            // Flush automático se o buffer atingir o tamanho limite
            if (buffer.Count >= _bufferSize)
            {
                FlushLogBuffer(logFilename).GetAwaiter().GetResult();
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[ERRO AO BUFFERIZAR LOG] {ex.Message}");
            Console.ResetColor();
        }
    }

    /// <summary>
    /// Descarrega entradas de log do buffer para o arquivo.
    /// </summary>
    private static async Task FlushLogBuffer(string logFilename)
    {
        if (!_logBuffers.TryGetValue(logFilename, out var buffer) || buffer.IsEmpty)
            return;

        var semaphore = _fileLocks.GetOrAdd(logFilename, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync();

        try
        {
            // Recria o diretório de logs, apenas por segurança
            Directory.CreateDirectory(Path.GetDirectoryName(logFilename));

            // Abre o arquivo para escrita com opções seguras para concorrência
            using (var writer = new StreamWriter(
                new FileStream(logFilename, FileMode.Append, FileAccess.Write, FileShare.ReadWrite)))
            {
                // Esvazia o buffer
                while (buffer.TryDequeue(out string logLine))
                {
                    await writer.WriteLineAsync(logLine);
                }

                await writer.FlushAsync();
            }
        }
        catch (IOException ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[ERRO AO ESCREVER LOG] {logFilename}: {ex.Message}");
            Console.ResetColor();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[ERRO INESPERADO AO ESCREVER LOG] {ex.Message}");
            Console.ResetColor();
        }
        finally
        {
            semaphore.Release();
        }
    }

    /// <summary>
    /// Callback para o timer que descarrega todos os buffers de log.
    /// </summary>
    private static void FlushAllLogs(object state)
    {
        foreach (var filename in _logBuffers.Keys)
        {
            FlushLogBuffer(filename).ConfigureAwait(false);
        }
    }

    #endregion
}

/// <summary>
/// Extensões para salvar informações de pacotes em arquivos.
/// </summary>
public static class PacketReaderExtensions
{
    /// <summary>
    /// Salva um pacote em um arquivo binário.
    /// </summary>
    public static async Task SaveAsync(byte[] packet, int Type, int Size)
    {
        var path = Path.Combine("Data", "Packets");
        try
        {
            Directory.CreateDirectory(path);

            string filename = Path.Combine(path, $"{Type}_{DateTime.Now:dd-MM-yyyy_HH-mm-ss}.bin");

            using (var fileStream = new FileStream(filename, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None))
            {
                await fileStream.WriteAsync(packet.AsMemory(0, Size));
            }
        }
        catch (Exception ex)
        {
            SysCons.LogError($"Erro ao salvar o pacote em {path}: {ex.Message}");
        }
    }
}

/*
Cores do Painel
Black
DarkBlue
DarkGreen
DarkCyan
DarkRed
DarkMagenta
DarkYellow
Gray
DarkGray
Blue
Green
Cyan
Red
Magenta
Yellow
White
*/