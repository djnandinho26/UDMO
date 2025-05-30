using DigitalWorldOnline.Commons.Interfaces;
using System;

namespace DigitalWorldOnline.Commons.Packet;

public static class SysCons
{
    private static string LogDirectory = "logs";

    public static void LogInfo(string text, params object[] args)
    {
        Console.ForegroundColor = ConsoleColor.White;
        ////Console.Write("[{0}] ", DateTime.Now.ToString(@"dd/MM/yyyy HH:mm:ss"));
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write("[INFO] ");
        Console.ForegroundColor = ConsoleColor.Gray;
        if (args.Length == 0)
            Console.WriteLine(text);
        else
            Console.WriteLine(text, args);
    }
    //COR do Login
    public static void Loggin(string text, params object[] args)
    {
        Console.ForegroundColor = ConsoleColor.White;
        //Console.Write("[{0}] ", DateTime.Now.ToString(@"dd/MM/yyyy HH:mm:ss"));
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write("[LOGIN] ");
        Console.ForegroundColor = ConsoleColor.Magenta;
        if (args.Length == 0)
            Console.WriteLine(text);
        else
            Console.WriteLine(text, args);
    }

    public static void LogWarn(string text, params object[] args)
    {
        Console.ForegroundColor = ConsoleColor.White;
        //Console.Write("[{0}] ", DateTime.Now.ToString(@"dd/MM/yyyy HH:mm:ss"));
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write("[WARN] ");
        Console.ForegroundColor = ConsoleColor.Gray;
        if (args.Length == 0)
            Console.WriteLine(text);
        else
            Console.WriteLine(text, args);
    }

    public static void LogIlegal(string text, params object[] args)
    {
        Console.ForegroundColor = ConsoleColor.White;
        //Console.Write("[{0}] ", DateTime.Now.ToString(@"dd/MM/yyyy HH:mm:ss"));
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write("[HACK] ");
        Console.ForegroundColor = ConsoleColor.Red;
        if (args.Length == 0)
            Console.WriteLine(text);
        else
            Console.WriteLine(text, args);
    }


    public static void LogDB(string text2, string text, params object[] args)
    {
        Console.ForegroundColor = ConsoleColor.White;
        //Console.Write("[{0}] ", DateTime.Now.ToString(@"dd/MM/yyyy HH:mm:ss"));
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write($"[{text2}] ");
        Console.ForegroundColor = ConsoleColor.Gray;
        if (args.Length == 0)
            Console.WriteLine(text);
        else
            Console.WriteLine(text, args);
    }

    public static void LogError(string text, params object[] args)
    {
        Console.ForegroundColor = ConsoleColor.White;
        //Console.Write("[{0}] ", DateTime.Now.ToString(@"dd/MM/yyyy HH:mm:ss"));
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Write("[ERROR] ");
        Console.ForegroundColor = ConsoleColor.Gray;
        if (args.Length == 0)
            Console.WriteLine(text);
        else
            Console.WriteLine(text, args);
    }
    public static void Chat(string text, params object[] args)
    {
        Console.ForegroundColor = ConsoleColor.White;
        //Console.Write("[{0}] ", DateTime.Now.ToString(@"dd/MM/yyyy HH:mm:ss"));
        Console.ForegroundColor = ConsoleColor.DarkRed;
        Console.Write("[CHAT] ");
        Console.ForegroundColor = ConsoleColor.Gray;
        if (args.Length == 0)
            Console.WriteLine(text);
        else
            Console.WriteLine(text, args);
    }

    public static void LogPacket(string text, params object[] args)
    {
        Console.ForegroundColor = ConsoleColor.White;
        //Console.Write("[{0}] ", DateTime.Now.ToString(@"dd/MM/yyyy HH:mm:ss"));
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Write("[PacketNull] ");
        Console.ForegroundColor = ConsoleColor.Blue;
        if (args.Length == 0)
            Console.WriteLine(text);
        else
            Console.WriteLine(text, args);
    }
    public static void LogPacketRecv(string text, params object[] args)
    {
        string logFilename = $"{LogDirectory}/LogPacketClient.txt";
        WriteLog(logFilename, "[PacketRecv]", text, args, ConsoleColor.DarkBlue, ConsoleColor.Gray);
    }
    public static void LogPacketSend(string text, params object[] args)
    {
        string logFilename = $"{LogDirectory}/LogPacketClient.txt";
        WriteLog(logFilename, "[PacketSend]", text, args, ConsoleColor.DarkBlue, ConsoleColor.Gray);
    }
    public static void LogPacketInvalid(string text, params object[] args)
    {
        string logFilename = $"{LogDirectory}/LogPacketClient.txt";
        WriteLog(logFilename, "[PacketInvalido]", text, args, ConsoleColor.DarkBlue, ConsoleColor.Gray);
    }


    private static void WriteLog(string logFilename, string logPrefix, string text, object[] args, ConsoleColor foregroundColor, ConsoleColor resetColor)
    {
        string logText;
        if (args.Length == 0)
            logText = text;
        else
            logText = string.Format(text, args);

        // Create directory if it doesn't exist
        Directory.CreateDirectory(LogDirectory);

        // Write log to file
        using (StreamWriter writer = new StreamWriter(logFilename, true))
        {
            writer.WriteLine($"{logPrefix} {logText}");
        }

        // Write log to console
        //Console.ForegroundColor = foregroundColor;
        //Console.Write($"{logPrefix} ");
        //Console.ForegroundColor = resetColor;
        //Console.WriteLine(logText);
    }
}
public static class PacketReaderExtensions
{
    public static async Task SaveAsync(byte[] packet,int Type,int Size)
    {
        var path = $@"Data\\Packets";
        try
        {
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

            using (var fileStream = new FileStream(Path.Combine(path, $@"{Type}_{DateTime.Now:dd-MM-yyyy_HH-mm-ss}.bin"),
                       FileMode.OpenOrCreate))
            {
                await fileStream.WriteAsync(packet.ToArray(), 0, Size);
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