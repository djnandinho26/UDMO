using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using FubarDev.FtpServer;
using FubarDev.FtpServer.FileSystem.DotNet;
using FubarDev.FtpServer.AccountManagement;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

class Program
{
    static async Task Main(string[] args)
    {
        // Caminho do diretório onde o servidor será executado
        string baseDir = AppContext.BaseDirectory;
        string ftpRootPath = Path.Combine(baseDir, "ftp_readonly");

        // Cria a pasta se não existir
        Directory.CreateDirectory(ftpRootPath);

        // Ajustar permissões para leitura
        SetReadOnly(ftpRootPath);

        // Configuração de autenticação
        var userName = "usuario";
        var password = "senha";

        // Configuração de IP e porta específicos
        var ipAddress = "192.168.0.66"; // Substitua pelo IP específico desejado como string
        var port = 2121; // Porta específica (não use a porta 21 se estiver ocupada)

        Console.WriteLine("Iniciando o servidor FTP...");
        Console.WriteLine($"Verifique se o IP {ipAddress} está configurado na sua máquina");

        // Listar IPs disponíveis para ajudar o usuário a escolher
        Console.WriteLine("Endereços IP disponíveis neste computador:");
        foreach (var ip in Dns.GetHostAddresses(Dns.GetHostName()))
        {
            if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork) // IPv4 apenas
            {
                Console.WriteLine($"- {ip}");
            }
        }

        // Configuração dos serviços
        var services = new ServiceCollection();

        // Adicionar logging
        services.AddLogging(logging => logging.AddConsole());

        // Configurar o sistema de arquivos
        services.Configure<DotNetFileSystemOptions>(opt =>
        {
            opt.RootPath = ftpRootPath;
        });

        // Configurar as opções do servidor FTP com IP e porta específicos
        services.Configure<FtpServerOptions>(opt =>
        {
            opt.ServerAddress = ipAddress; // Agora usando string diretamente
            opt.Port = port;
        });

        // Adicionar o servidor FTP com as configurações necessárias
        services.AddFtpServer(builder => builder.UseDotNetFileSystem());

        // Registrar nosso provedor de membros personalizado
        services.AddSingleton<IMembershipProvider, ReadOnlyMembershipProvider>(
            provider => new ReadOnlyMembershipProvider(userName, password));

        // Construir o provedor de serviços
        var serviceProvider = services.BuildServiceProvider();

        // Obter o host do servidor FTP
        var ftpServerHost = serviceProvider.GetRequiredService<IFtpServerHost>();

        try
        {
            // Iniciar o servidor
            await ftpServerHost.StartAsync();

            Console.WriteLine("Servidor FTP iniciado com sucesso!");
            Console.WriteLine($"Endereço IP: {ipAddress}");
            Console.WriteLine($"Porta: {port}");
            Console.WriteLine($"Acesso somente leitura na pasta: {ftpRootPath}");
            Console.WriteLine($"Login: {userName} | Senha: {password}");
            Console.WriteLine("Pressione qualquer tecla para parar...");
            Console.ReadKey();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao iniciar o servidor FTP: {ex.Message}");
            Console.WriteLine("Verifique se o IP está disponível e se a porta não está em uso.");
            Console.WriteLine("Pressione qualquer tecla para sair...");
            Console.ReadKey();
        }
        finally
        {
            // Parar o servidor
            await ftpServerHost.StopAsync();
        }
    }

    // Método para ajustar permissões do sistema de arquivos para somente leitura
    static void SetReadOnly(string path)
    {
        try
        {
            var dirInfo = new DirectoryInfo(path);
            dirInfo.Attributes = FileAttributes.ReadOnly;

            // Para arquivos dentro da pasta, ajustar permissões também
            foreach (var file in dirInfo.GetFiles("*", SearchOption.AllDirectories))
            {
                file.Attributes = FileAttributes.ReadOnly;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao definir atributos: {ex.Message}");
        }
    }
}

// Provedor de membros somente leitura
public class ReadOnlyMembershipProvider : IMembershipProvider
{
    private readonly string _username;
    private readonly string _password;

    public ReadOnlyMembershipProvider(string username, string password)
    {
        _username = username;
        _password = password;
    }

    // Método corrigido para usar ValidateUserAsync como Task
    public Task<MemberValidationResult> ValidateUserAsync(string username, string password)
    {
        if (username == _username && password == _password)
        {
            // Criar um usuário somente leitura
            return Task.FromResult(new MemberValidationResult(
                MemberValidationStatus.AuthenticatedUser,
                new ReadOnlyFtpUser(username)));
        }

        // Retorna status para login inválido
        return Task.FromResult(new MemberValidationResult(MemberValidationStatus.InvalidLogin));
    }
}

// Implementação de usuário somente leitura
public class ReadOnlyFtpUser : IFtpUser
{
    public ReadOnlyFtpUser(string userName)
    {
        Name = userName;
    }

    public string Name { get; }

    public bool IsInGroup(string groupName)
    {
        return groupName == "readonly" || groupName == Name;
    }
}