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
using DigitalWorldOnline.Application.Separar.Commands.Delete;
using DigitalWorldOnline.Commons.Models.Base;
using DigitalWorldOnline.Commons.Packets.GameServer;

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
                
                 //---- !done command ----------------------
                case "done":
                    {
                        var regex = @"^done";
                        var match = Regex.IsMatch(message, regex, RegexOptions.IgnoreCase);

                        if (!match)
                        {
                            client.Send(new SystemMessagePacket($"Unknown command.\nType !done (slot)"));
                            break;
                        }

                        if (command.Length < 2)
                        {
                            client.Send(new SystemMessagePacket("Invalid command.\nType !delete (slot)"));
                            break;
                        }

                        if (!byte.TryParse(command[1], out byte digiSlot))
                        {
                            client.Send(new SystemMessagePacket("Invalid Slot.\nType a valid Slot (1 to 4)"));
                            break;
                        }

                        if (digiSlot == 0 || digiSlot > 4)
                        {
                            client.Send(new SystemMessagePacket($"Invalid Slot !!"));
                            break;
                        }

                        var digimon = client.Tamer.Digimons.FirstOrDefault(x => x.Slot == digiSlot);

                        if (digimon == null)
                        {
                            client.Send(new SystemMessagePacket($"Digimon not found on slot {digiSlot}"));
                            break;
                        }
                        else
                        {
                            var digimonId = digimon.Id;

                            if (digimon.BaseType == 31066 && digimon.Level >= 99)
                            {
                                var itemId = 66935; // 66935 impmon item

                                var newItem = new ItemModel();
                                newItem.SetItemInfo(_assets.ItemInfo.FirstOrDefault(x => x.ItemId == itemId));

                                if (newItem.ItemInfo == null)
                                {
                                    _logger.Warning($"No item info found with ID {itemId} for tamer {client.TamerId}.");
                                    client.Send(new SystemMessagePacket($"No item info found with ID {itemId}."));
                                    break;
                                }

                                newItem.ItemId = itemId;
                                newItem.Amount = 1;

                                if (newItem.IsTemporary)
                                    newItem.SetRemainingTime((uint)newItem.ItemInfo.UsageTimeMinutes);

                                var itemClone = (ItemModel)newItem.Clone();

                                if (client.Tamer.Inventory.AddItem(newItem))
                                {
                                    client.Send(new ReceiveItemPacket(newItem, InventoryTypeEnum.Inventory));
                                    await _sender.Send(new UpdateItemsCommand(client.Tamer.Inventory));
                                }
                                else
                                {
                                    client.Send(new PickItemFailPacket(PickItemFailReasonEnum.InventoryFull));
                                    client.Send(new SystemMessagePacket($"Inventory full !!"));
                                    break;
                                }

                                client.Tamer.RemoveDigimon(digiSlot);

                                client.Send(new PartnerDeletePacket(digiSlot));

                                await _sender.Send(new DeleteDigimonCommand(digimonId));
                            }
                            else if (digimon.BaseType == 31023 && digimon.Level >= 99) // Sleipmon Type
                            {
                                var itemId = 66936; // 66936 slepymon item

                                var newItem = new ItemModel();
                                newItem.SetItemInfo(_assets.ItemInfo.FirstOrDefault(x => x.ItemId == itemId));

                                if (newItem.ItemInfo == null)
                                {
                                    _logger.Warning($"No item info found with ID {itemId} for tamer {client.TamerId}.");
                                    client.Send(new SystemMessagePacket($"No item info found with ID {itemId}."));
                                    break;
                                }

                                newItem.ItemId = itemId;
                                newItem.Amount = 1;

                                if (newItem.IsTemporary)
                                    newItem.SetRemainingTime((uint)newItem.ItemInfo.UsageTimeMinutes);

                                var itemClone = (ItemModel)newItem.Clone();

                                if (client.Tamer.Inventory.AddItem(newItem))
                                {
                                    client.Send(new ReceiveItemPacket(newItem, InventoryTypeEnum.Inventory));
                                    await _sender.Send(new UpdateItemsCommand(client.Tamer.Inventory));
                                }
                                else
                                {
                                    client.Send(new PickItemFailPacket(PickItemFailReasonEnum.InventoryFull));
                                    client.Send(new SystemMessagePacket($"Inventory full !!"));
                                    break;
                                }

                                client.Tamer.RemoveDigimon(digiSlot);

                                client.Send(new PartnerDeletePacket(digiSlot));

                                await _sender.Send(new DeleteDigimonCommand(digimonId));
                            }
                            else if (digimon.BaseType == 31022 && digimon.Level >= 99) // Dynasmon Type
                            {
                                var itemId = 66937; // 66937 dynasmon item

                                var newItem = new ItemModel();
                                newItem.SetItemInfo(_assets.ItemInfo.FirstOrDefault(x => x.ItemId == itemId));

                                if (newItem.ItemInfo == null)
                                {
                                    _logger.Warning($"No item info found with ID {itemId} for tamer {client.TamerId}.");
                                    client.Send(new SystemMessagePacket($"No item info found with ID {itemId}."));
                                    break;
                                }

                                newItem.ItemId = itemId;
                                newItem.Amount = 1;

                                if (newItem.IsTemporary)
                                    newItem.SetRemainingTime((uint)newItem.ItemInfo.UsageTimeMinutes);

                                var itemClone = (ItemModel)newItem.Clone();

                                if (client.Tamer.Inventory.AddItem(newItem))
                                {
                                    client.Send(new ReceiveItemPacket(newItem, InventoryTypeEnum.Inventory));
                                    await _sender.Send(new UpdateItemsCommand(client.Tamer.Inventory));
                                }
                                else
                                {
                                    client.Send(new PickItemFailPacket(PickItemFailReasonEnum.InventoryFull));
                                    client.Send(new SystemMessagePacket($"Inventory full !!"));
                                    break;
                                }

                                client.Tamer.RemoveDigimon(digiSlot);

                                client.Send(new PartnerDeletePacket(digiSlot));

                                await _sender.Send(new DeleteDigimonCommand(digimonId));
                            }
                            else
                            {
                                client.Send(new SystemMessagePacket("Wrong digimon type or level less than 99!!"));
                                break;
                            }

                        }

                    }
                    break;

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
                        else if (command[1] == "Done")
                        {
                            client.Send(new SystemMessagePacket("!Done: is used to sacrifise the digimon you want to get ruin item"));
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

                
            }
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }
}
