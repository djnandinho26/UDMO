using AutoMapper;
using DigitalWorldOnline.Application.Separar.Commands.Create;
using DigitalWorldOnline.Application.Separar.Queries;
using DigitalWorldOnline.Commons.Entities;
using DigitalWorldOnline.Commons.Enums;
using DigitalWorldOnline.Commons.Enums.PacketProcessor;
using DigitalWorldOnline.Commons.Extensions;
using DigitalWorldOnline.Commons.Interfaces;
using DigitalWorldOnline.Commons.Models.Account;
using DigitalWorldOnline.Commons.Models.Asset;
using DigitalWorldOnline.Commons.Models.Character;
using DigitalWorldOnline.Commons.Models.Digimon;
using DigitalWorldOnline.Commons.Packets.CharacterServer;
using DigitalWorldOnline.Commons.Utils;
using DigitalWorldOnline.Commons.Writers;
using MediatR;
using Microsoft.Extensions.Configuration;
using Serilog;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DigitalWorldOnline.Character.PacketProcessors
{
    internal class RecvCreateTamer : ICharacterPacketProcessor
    {
        public CharacterServerPacketEnum Type => CharacterServerPacketEnum.CreateCharacter;

        private readonly ISender _sender;
        private readonly IMapper _mapper;
        private readonly ILogger _logger;
        private readonly IConfiguration _configuration;

        private const int HandshakeStampDegree = 65535;
        private const int NameMaxLength = 66;
        private const int NameCharacterLimit = 12; // Limite de 12 caracteres para nomes

        // Regex para caracteres válidos (letras, números e alguns caracteres especiais básicos - SEM ESPAÇOS)
        private static readonly Regex ValidCharacters = new Regex(@"^[a-zA-Z0-9À-ÿ\u00C0-\u017F._-]+$", RegexOptions.Compiled);

        public RecvCreateTamer(
            ISender sender,
            IMapper mapper,
            ILogger logger,
            IConfiguration configuration)
        {
            _sender = sender ?? throw new ArgumentNullException(nameof(sender));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        /// <summary>  
        /// Processa a requisição de criação de personagem do cliente.  
        /// </summary>  
        /// <param name="client">Cliente do jogo que enviou o pacote</param>  
        /// <param name="packetData">Dados do pacote recebido</param>  
        /// <returns>Task representando a operação assíncrona</returns>  
        public async Task Process(GameClient client, byte[] packetData)
        {
            // Validação dos parâmetros de entrada  
            ArgumentNullException.ThrowIfNull(client, nameof(client));
            ArgumentNullException.ThrowIfNull(packetData, nameof(packetData));

            try
            {
                _logger.Debug("Processando requisição de criação de domador de {ClientAddress}", client.ClientAddress);

                // Verifica se há dados suficientes para processar
                var expectedSize = 1 + 4 + NameMaxLength + 4 + NameMaxLength; // position + tamerModel + tamerName + digimonModel + digimonName
                if (packetData.Length < expectedSize)
                {
                    _logger.Warning("Pacote de criação de personagem muito pequeno: {Length} bytes, esperado: {Expected}",
                        packetData.Length, expectedSize);
                    client.Send(new CharacterCreationErrorPacket("Dados insuficientes no pacote."));
                    return;
                }

                // Cria um leitor de pacotes para processar os dados recebidos  
                using var stream = new MemoryStream(packetData);
                using var reader = new BinaryReader(stream);

                _logger.Debug("Lendo parâmetros do pacote...");
                reader.ReadByte();
                var tamerModel = reader.ReadInt32();
                var tamerName = ReadAndValidateString(reader, NameMaxLength, "domador");
                var digimonModel = reader.ReadInt32();
                var digimonName = ReadAndValidateString(reader, NameMaxLength, "digimon");

                // Validações adicionais
                if (!ValidateCharacterData(tamerModel, tamerName, digimonModel, digimonName, client))
                {
                    return;
                }

                _logger.Debug($"Buscando conta com ID {client.AccountId}...");
                var account = _mapper.Map<AccountModel>(await _sender.Send(new AccountByIdQuery(client.AccountId)));

                if (account == null)
                {
                    _logger.Warning("Conta não encontrada para ID {AccountId}", client.AccountId);
                    client.Send(new CharacterCreationErrorPacket("Conta não encontrada."));
                    return;
                }

                // Aplica prefixo de moderador se necessário
                //tamerName = tamerName.ModeratorPrefix(account.AccessLevel);

                // Verifica se o nome já existe
                var existingCharacter = await _sender.Send(new CharacterByNameQuery(tamerName));
                if (existingCharacter != null)
                {
                    _logger.Warning("Nome de domador '{TamerName}' já existe", tamerName);
                    client.Send(new CharacterCreationErrorPacket("Nome já está em uso."));
                    return;
                }

                _logger.Debug("Criando personagem...");
                var character = CharacterModel.Create(
                    client.AccountId,
                    tamerName,
                    tamerModel,
                    account.LastPlayedServer);

                _logger.Debug("Criando digimon...");
                var digimon = DigimonModel.Create(
                    digimonName,
                    digimonModel,
                    digimonModel,
                    DigimonHatchGradeEnum.Perfect,
                    UtilitiesFunctions.RandomShort(12000, 12000),
                    0);

                character.AddDigimon(digimon);

                var handshakeTimestamp = (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var handshake = (short)(handshakeTimestamp & HandshakeStampDegree);

                // Obtém informações de status do domador
                _logger.Debug("Obtendo informações de status do domador...");
                character.SetBaseStatus(
                    _mapper.Map<CharacterBaseStatusAssetModel>(
                        await _sender.Send(new TamerBaseStatusQuery(character.Model))));

                character.SetLevelStatus(
                    _mapper.Map<CharacterLevelStatusAssetModel>(
                        await _sender.Send(new TamerLevelStatusQuery(character.Model, character.Level))));

                character.Partner.SetBaseInfo(
                    _mapper.Map<DigimonBaseInfoAssetModel>(
                        await _sender.Send(new DigimonBaseInfoQuery(character.Partner.CurrentType))));

                _logger.Debug($"Registrando domador e digimon para a conta {account.Username}...");
                character.Partner.AddEvolutions(await _sender.Send(new DigimonEvolutionAssetsByTypeQuery(digimonModel)));

                var characterId = await _sender.Send(new CreateCharacterCommand(character));

                if (characterId > 0)
                {
                    _logger.Information("Personagem '{TamerName}' criado com sucesso para a conta {Username}",
                        tamerName, account.Username);
                    client.Send(new CharacterCreatedPacket(character, handshake));
                }
                else
                {
                    _logger.Error("Falha ao criar personagem no banco de dados");
                    client.Send(new CharacterCreationErrorPacket("Erro interno do servidor."));
                }
            }
            catch (EndOfStreamException ex)
            {
                _logger.Error(ex, "Dados insuficientes no pacote de criação de personagem de {ClientAddress}: {Message}",
                    client.ClientAddress, ex.Message);
                client.Send(new CharacterCreationErrorPacket("Dados do pacote incompletos."));
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Erro ao processar requisição de criação de personagem de {ClientAddress}: {Message}",
                    client.ClientAddress, ex.Message);
                client.Send(new CharacterCreationErrorPacket("Erro interno do servidor."));
            }
        }

        /// <summary>
        /// Lê e valida uma string de tamanho fixo do pacote, processando primeiro os 66 bytes completos e depois filtrando para 12 caracteres
        /// </summary>
        private string ReadAndValidateString(BinaryReader reader, int maxLength, string fieldName)
        {
            // Lê os 66 bytes completos primeiro
            byte[] nameBytes = reader.ReadBytes(maxLength);

            // Detecta o encoding e decodifica a string apropriadamente
            string rawString = DetectAndDecodeString(nameBytes);

            // Remove caracteres nulos do final da string
            string cleanString = rawString.TrimEnd('\0');

            // Se ainda estiver vazio, tenta extrair até o primeiro espaço ou nulo (fallback)
            if (string.IsNullOrEmpty(cleanString))
            {
                cleanString = ExtractStringUntilSpaceOrNull(rawString);
            }

            // Aplica filtros de limpeza e validação
            string validatedString = CleanAndValidateString(cleanString, fieldName);

            _logger.Debug("Campo {FieldName}: Raw bytes -> '{RawString}' -> '{ValidatedString}'",
                fieldName, rawString.Replace("\0", "\\0").Replace(" ", "\\s"), validatedString);

            return validatedString;
        }

        /// <summary>
        /// Extrai a string até encontrar um espaço ou caractere nulo
        /// </summary>
        private string ExtractStringUntilSpaceOrNull(string input)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            // Encontra a primeira ocorrência de espaço ou caractere nulo
            int endIndex = -1;
            for (int i = 0; i < input.Length; i++)
            {
                if (input[i] == ' ' || input[i] == '\0')
                {
                    endIndex = i;
                    break;
                }
            }

            // Se encontrou espaço ou null, retorna a substring até esse ponto
            if (endIndex >= 0)
            {
                return input.Substring(0, endIndex);
            }

            // Se não encontrou, retorna a string completa
            return input;
        }

        /// <summary>
        /// Detecta o encoding e decodifica a string apropriadamente
        /// </summary>
        private string DetectAndDecodeString(byte[] bytes)
        {
            // Verifica se parece UTF-16 (muitos bytes nulos alternados)
            int nullCount = bytes.Count(b => b == 0);
            if (nullCount > bytes.Length / 3) // Se mais de 1/3 são zeros, provavelmente UTF-16
            {
                try
                {
                    return Encoding.Unicode.GetString(bytes);
                }
                catch
                {
                    // Se falhar, usa UTF-8 como fallback
                }
            }

            return Encoding.UTF8.GetString(bytes);
        }

        /// <summary>
        /// Limpa e valida a string removendo caracteres inválidos e aplicando limite de 12 caracteres
        /// </summary>
        private string CleanAndValidateString(string input, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                throw new ArgumentException($"Nome de {fieldName} não pode estar vazio.", nameof(input));
            }

            // Remove caracteres de controle
            string cleaned = new string(input.Where(c => !char.IsControl(c)).ToArray());

            // Remove espaços
            cleaned = cleaned.Replace(" ", "");

            // Valida caracteres permitidos
            if (!ValidCharacters.IsMatch(cleaned))
            {
                // Remove caracteres não permitidos, mantendo apenas os válidos
                cleaned = Regex.Replace(cleaned, @"[^a-zA-Z0-9À-ÿ\u00C0-\u017F._-]", "");

                if (string.IsNullOrEmpty(cleaned))
                {
                    throw new ArgumentException($"Nome de {fieldName} contém apenas caracteres inválidos.", nameof(input));
                }
            }

            // PRIMEIRO: Aplica limite de 12 caracteres ANTES da validação de comprimento mínimo
            if (cleaned.Length > NameCharacterLimit)
            {
                cleaned = cleaned.Substring(0, NameCharacterLimit);
                _logger.Debug("Nome de {FieldName} truncado para {Limit} caracteres: '{TruncatedName}'",
                    fieldName, NameCharacterLimit, cleaned);
            }

            // DEPOIS: Valida comprimento mínimo
            if (cleaned.Length < 2)
            {
                _logger.Warning("Nome de {FieldName} muito curto após limpeza: '{CleanedName}' (original: '{OriginalInput}')",
                    fieldName, cleaned, input);

                // Tentativa de recuperação: usa apenas caracteres alfanuméricos
                if (!string.IsNullOrEmpty(input) && input.Trim().Length >= 2)
                {
                    string recovered = Regex.Replace(input, @"[^a-zA-Z0-9]", "");

                    // Aplica o limite de 12 caracteres também na recuperação
                    if (recovered.Length > NameCharacterLimit)
                    {
                        recovered = recovered.Substring(0, NameCharacterLimit);
                    }

                    if (recovered.Length >= 2)
                    {
                        _logger.Information("Recuperado nome de {FieldName} usando limpeza alternativa: '{RecoveredName}'",
                            fieldName, recovered);
                        cleaned = recovered;
                    }
                }

                // Se ainda for muito curto após tentativa de recuperação, lança exceção
                if (cleaned.Length < 2)
                {
                    throw new ArgumentException($"Nome de {fieldName} deve ter pelo menos 2 caracteres. Nome processado: '{cleaned}', nome original: '{input}'.", nameof(input));
                }
            }

            // Normaliza a string para a cultura pt-BR
            cleaned = NormalizeForPortuguese(cleaned);

            return cleaned;
        }

        /// <summary>
        /// Normaliza a string para padrões brasileiros
        /// </summary>
        private string NormalizeForPortuguese(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            // Normaliza para NFC (Canonical Decomposition followed by Canonical Composition)
            string normalized = input.Normalize(NormalizationForm.FormC);

            // Aplica capitalização apropriada (primeira letra maiúscula)
            var textInfo = new CultureInfo("pt-BR", false).TextInfo;
            normalized = textInfo.ToTitleCase(normalized.ToLower());

            return normalized;
        }

        /// <summary>
        /// Valida os dados do personagem
        /// </summary>
        private bool ValidateCharacterData(int tamerModel, string tamerName,
            int digimonModel, string digimonName, GameClient client)
        {

            // Valida modelo do domador
            if (tamerModel < 80001 || tamerModel > 80004) // Assumindo modelos válidos de 80001 a 80004
            {
                _logger.Warning("Modelo de domador inválido: {TamerModel}", tamerModel);
                client.Send(new CharacterCreationErrorPacket("Modelo de domador inválido."));
                return false;
            }

            // Valida modelo do digimon
            if (digimonModel < 31001 || digimonModel > 31004) // Assumindo que IDs de digimon começam em 31001 ate 31004
            {
                _logger.Warning("Modelo de digimon inválido: {DigimonModel}", digimonModel);
                client.Send(new CharacterCreationErrorPacket("Modelo de digimon inválido."));
                return false;
            }

            // Valida se os nomes foram processados corretamente
            if (string.IsNullOrEmpty(tamerName) || string.IsNullOrEmpty(digimonName))
            {
                _logger.Warning("Nomes inválidos após validação. Domador: '{TamerName}', Digimon: '{DigimonName}'",
                    tamerName, digimonName);
                client.Send(new CharacterCreationErrorPacket("Nomes de personagem inválidos."));
                return false;
            }

            // Valida comprimento dos nomes após processamento
            if (tamerName.Length > NameCharacterLimit || digimonName.Length > NameCharacterLimit)
            {
                _logger.Warning("Nomes excedem o limite de {Limit} caracteres. Domador: {TamerLength}, Digimon: {DigimonLength}",
                    NameCharacterLimit, tamerName.Length, digimonName.Length);
                client.Send(new CharacterCreationErrorPacket($"Nomes devem ter no máximo {NameCharacterLimit} caracteres."));
                return false;
            }

            return true;
        }
    }

    /// <summary>
    /// Pacote de erro de criação de personagem
    /// </summary>
    public class CharacterCreationErrorPacket : PacketWriter
    {
        private const int PacketNumber = 1303; // Assumindo um número de pacote para erro

        public CharacterCreationErrorPacket(string errorMessage)
        {
            Type(PacketNumber);
            WriteByte(0); // Status de erro
            WriteString(errorMessage);
        }
    }
}