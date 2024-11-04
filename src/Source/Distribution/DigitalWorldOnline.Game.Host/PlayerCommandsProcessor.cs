using DigitalWorldOnline.Application;
using DigitalWorldOnline.Application.Separar.Commands.Update;
using DigitalWorldOnline.Commons.Entities;
using DigitalWorldOnline.Commons.Enums.ClientEnums;
using DigitalWorldOnline.Commons.Packets.Chat;
using DigitalWorldOnline.Commons.Packets.GameServer.Combat;
using DigitalWorldOnline.Commons.Packets.Items;
using DigitalWorldOnline.Game.Managers;
using DigitalWorldOnline.GameHost;
using MediatR;
using Microsoft.Extensions.Configuration;
using Serilog;
using System.Text.RegularExpressions;

namespace DigitalWorldOnline.Game
{
    public sealed class PlayerCommandsProcessor : IDisposable
    {
        private const string GameServerAddress = "GameServer:Address";
        private const string GamerServerPublic = "GameServer:PublicAddress";
        private const string GameServerPort = "GameServer:Port";

        private readonly PartyManager _partyManager;
        private readonly StatusManager _statusManager;
        private readonly ExpManager _expManager;
        private readonly AssetsLoader _assets;
        private readonly MapServer _mapServer;
        private readonly DungeonsServer _dungeonServer;
        private readonly PvpServer _pvpServer;
        private readonly ILogger _logger;
        private readonly ISender _sender;
        private readonly IConfiguration _configuration;

        public PlayerCommandsProcessor(
            PartyManager partyManager,
            StatusManager statusManager,
            ExpManager expManager,
            AssetsLoader assets,
            MapServer mapServer,
            DungeonsServer dungeonsServer,
            PvpServer pvpServer,
            ILogger logger,
            ISender sender,
            IConfiguration configuration)
        {
            _partyManager = partyManager;
            _expManager = expManager;
            _statusManager = statusManager;
            _assets = assets;
            _mapServer = mapServer;
            _dungeonServer = dungeonsServer;
            _pvpServer = pvpServer;
            _logger = logger;
            _sender = sender;
            _configuration = configuration;
        }

        public async Task ExecuteCommand(GameClient client, string message)
        {
            var command = Regex.Replace(message.Trim().ToLower(), @"\s+", " ").Split(' ');
            _logger.Information($"Player AccountID: {client.AccountId} Tamer: {client.Tamer.Name} used Command !{message}");

            switch (command[0])
            {
                case "clear":
                    {
                        var regex = @"^clear\s+(inv|cash|gift)$";
                        var match = Regex.Match(message, regex, RegexOptions.IgnoreCase);

                        if (!match.Success)
                        {
                            client.Send(new SystemMessagePacket("Unknown command.\nType !clear {inv|cash|gift}\n"));
                            break;
                        }

                        if (command[1] == "inv")
                        {
                            client.Tamer.Inventory.Clear();

                            client.Send(new SystemMessagePacket($" Inventory slots cleaned."));
                            client.Send(new LoadInventoryPacket(client.Tamer.Inventory, InventoryTypeEnum.Inventory));

                            await _sender.Send(new UpdateItemsCommand(client.Tamer.Inventory));
                        }
                        else if (command[1] == "cash")
                        {
                            client.Tamer.AccountCashWarehouse.Clear();

                            client.Send(new SystemMessagePacket($" CashStorage slots cleaned."));
                            client.Send(new LoadAccountWarehousePacket(client.Tamer.AccountCashWarehouse));

                            await _sender.Send(new UpdateItemsCommand(client.Tamer.AccountCashWarehouse));
                        }
                        else if (command[1] == "gift")
                        {
                            client.Tamer.GiftWarehouse.Clear();

                            client.Send(new SystemMessagePacket($" GiftStorage slots cleaned."));
                            client.Send(new LoadGiftStoragePacket(client.Tamer.GiftWarehouse));

                            await _sender.Send(new UpdateItemsCommand(client.Tamer.GiftWarehouse));
                        }

                    }
                    break;

                case "stats":
                    {
                        var regex = @"^stats\s*$";
                        var match = Regex.Match(message, regex, RegexOptions.IgnoreCase);

                        if (!match.Success)
                        {
                            client.Send(new SystemMessagePacket($"Unknown command.\nType !stats"));
                            break;
                        }

                        client.Send(new SystemMessagePacket($"Critical Damage: {client.Tamer.Partner.CD / 100}%\n" +
                            $"Attribute Damage: {client.Tamer.Partner.ATT / 100}%\n" +
                            $"Digimon SKD: {client.Tamer.Partner.SKD}\n" +
                            $"Digimon SCD: {client.Tamer.Partner.SCD / 100}%\n" +
                            $"Tamer BonusEXP: {client.Tamer.BonusEXP}%\n" +
                            $"Tamer Move Speed: {client.Tamer.MS}"));

                    }
                    break;

                case "time":
                    {
                        var regex = @"^time\s*$";
                        var match = Regex.Match(message, regex, RegexOptions.IgnoreCase);

                        if (!match.Success)
                        {
                            client.Send(new SystemMessagePacket($"Unknown command.\nType !time"));
                            break;
                        }

                        client.Send(new SystemMessagePacket($"Server Time is: {DateTime.UtcNow}"));
                    }
                    break;

                // --- HELP -------------------------------

                case "help":
                    {
                        if (command[1] == "inv")
                        {
                            client.Send(new SystemMessagePacket("!clear inv: Clear your inventory"));
                        }
                        else if (command[1] == "cash")
                        {
                            client.Send(new SystemMessagePacket("!clear cash: Clear your CashStorage"));
                        }
                        else if (command[1] == "gift")
                        {
                            client.Send(new SystemMessagePacket("!clear gift: Clear your GiftStorage"));
                        }
                        else if (command[1] == "stats")
                        {
                            client.Send(new SystemMessagePacket("!stats: Show hidden stats"));
                        }
                        else if (command[1] == "time")
                        {
                            client.Send(new SystemMessagePacket("!time: Show the server time"));
                        }
                        else
                        {
                            client.Send(new SystemMessagePacket("Commands:\n1. !clear\n2. !stats\n3. !time\nType !help {command} for more details."));
                        }
                    }
                    break;

                // --- PVP --------------------------------

                #region Pvp

                /*case "pvp":
                    {
                        var regex = @"(pvp\son){1}|(pvp\soff){1}";
                        var match = Regex.Match(message, regex, RegexOptions.IgnoreCase);

                        if (!match.Success)
                        {
                            client.Send(new SystemMessagePacket($"Unknown command.\nType !pvp (on/off)"));
                            break;
                        }

                        if (client.Tamer.InBattle)
                        {
                            client.Send(new SystemMessagePacket($"You can't turn off pvp on battle !"));
                            break;
                        }

                        switch (command[1])
                        {
                            case "on":
                                {
                                    if (client.Tamer.PvpMap == false)
                                    {
                                        client.Tamer.PvpMap = true;
                                        client.Send(new NoticeMessagePacket($"PVP turned on !!"));
                                    }
                                    else client.Send(new NoticeMessagePacket($"PVP is already on ..."));
                                }
                                break;

                            case "off":
                                {
                                    if (client.Tamer.PvpMap == true)
                                    {
                                        client.Tamer.PvpMap = false;
                                        client.Send(new NoticeMessagePacket($"PVP turned off !!"));
                                    }
                                    else client.Send(new NoticeMessagePacket($"PVP is already off ..."));
                                }
                                break;
                        }
                    }
                    break;*/

                #endregion

                // --- DECK -------------------------------

                case "deckload":
                    {
                        var regex = @"^deckload\s*$";
                        var match = Regex.Match(message, regex, RegexOptions.IgnoreCase);

                        if (!match.Success)
                        {
                            client.Send(new SystemMessagePacket($"Unknown command.\nType !deckload"));
                            break;
                        }

                        var evolution = client.Partner.Evolutions[0];

                        _logger.Information($"Evolution ID: {evolution.Id} | Evolution Type: {evolution.Type} | Evolution Unlocked: {evolution.Unlocked}");

                        var evoInfo = _assets.EvolutionInfo.FirstOrDefault(x => x.Type == client.Partner.BaseType)?.Lines.FirstOrDefault(x => x.Type == evolution.Type);

                        _logger.Information($"EvoInfo ID: {evoInfo.Id}");
                        _logger.Information($"EvoInfo EvolutionId: {evoInfo.EvolutionId}");
                    }
                    break;

                // --- DEFAULT ----------------------------

                default:
                    client.Send(new SystemMessagePacket($"Invalid Command !!\nType !help"));
                    break;
            }
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }
}
