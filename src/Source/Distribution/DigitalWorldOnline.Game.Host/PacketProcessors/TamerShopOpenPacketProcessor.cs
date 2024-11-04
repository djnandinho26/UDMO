using DigitalWorldOnline.Application;
using DigitalWorldOnline.Application.Separar.Commands.Update;
using DigitalWorldOnline.Commons.Entities;
using DigitalWorldOnline.Commons.Enums.ClientEnums;
using DigitalWorldOnline.Commons.Enums.PacketProcessor;
using DigitalWorldOnline.Commons.Extensions;
using DigitalWorldOnline.Commons.Interfaces;
using DigitalWorldOnline.Commons.Models.Base;
using DigitalWorldOnline.Commons.Packets.PersonalShop;
using DigitalWorldOnline.Commons.Packets.GameServer;
using DigitalWorldOnline.Commons.Packets.Items;
using DigitalWorldOnline.GameHost;
using MediatR;
using Serilog;
using DigitalWorldOnline.Commons.Packets.Chat;

namespace DigitalWorldOnline.Game.PacketProcessors
{
    public class TamerShopOpenPacketProcessor : IGamePacketProcessor
    {
        public GameServerPacketEnum Type => GameServerPacketEnum.TamerShopOpen;

        private readonly MapServer _mapServer;
        private readonly AssetsLoader _assets;
        private readonly ILogger _logger;
        private readonly ISender _sender;

        public TamerShopOpenPacketProcessor(MapServer mapServer, AssetsLoader assets, ILogger logger, ISender sender)
        {
            _mapServer = mapServer;
            _assets = assets;
            _logger = logger;
            _sender = sender;
        }

        public async Task Process(GameClient client, byte[] packetData)
        {
            var packet = new GamePacketReader(packetData);

            _logger.Debug($"--- PersonalShop Open Packet 1511 ---\n");
            
            var shopName = packet.ReadString();

            packet.Skip(1);

            var sellQuantity = packet.ReadInt();

            //_logger.Information($"Shop Location: Map {client.Tamer.Location.MapId} ShopName: {shopName}, Items Amount: {sellQuantity}\n");
            
            List<ItemModel> sellList = new(sellQuantity);

            //_logger.Information($"-------------------------------------\n");

            for (int i = 0; i < sellQuantity; i++)
            {
                var itemId = packet.ReadInt();
                var itemAmount = packet.ReadInt();

                //_logger.Information($"Item Index: {i} | ItemId: {itemId} | ItemAmount: {itemAmount}");

                var sellItem = new ItemModel(itemId, itemAmount);

                packet.Skip(64);

                var price = packet.ReadInt64();
                sellItem.SetSellPrice(price);

                packet.Skip(8);

                //_logger.Information("--- Pacote Bruto ---");
                //string packetHex = BitConverter.ToString(packetData);
                //_logger.Information(packetHex);
                //_logger.Information("--------------------");
                
                sellList.Add(sellItem);

                //_logger.Information($"Item Index: {i} | Price: {price}\n");
            }

            //_logger.Information($"-------------------------------------");

            foreach (var item in sellList)
            {
                // Verification for selling the same item with different price
                item.SetItemInfo(_assets.ItemInfo.First(x => x.ItemId == item.ItemId));

                foreach (var item2 in sellList)
                {
                    if (item2.ItemId == item.ItemId && item2.TamerShopSellPrice != item.TamerShopSellPrice)
                    {
                        //client.Send(new DisconnectUserPacket("You cant add 2 items of same id with different price!").Serialize());
                        _logger.Warning($"Tamer {client.Tamer.Name} tryed to sell 2 items of same id with different price !!");
                        client.Send(new SystemMessagePacket($"You cant add 2 items of same id\n with different price!\n ShopClosed !!"));

                        client.Tamer.UpdateCurrentCondition(ConditionEnum.Default);

                        client.Tamer.Inventory.AddItems(client.Tamer.TamerShop.Items);
                        client.Tamer.TamerShop.Clear();

                        client.Send(new PersonalShopPacket());

                        await _sender.Send(new UpdateItemsCommand(client.Tamer.Inventory));
                        await _sender.Send(new UpdateItemsCommand(client.Tamer.TamerShop));

                        client.Send(new LoadInventoryPacket(client.Tamer.Inventory, InventoryTypeEnum.Inventory).Serialize());

                        _mapServer.BroadcastForTamerViewsAndSelf(client.TamerId, new SyncConditionPacket(client.Tamer.GeneralHandler, client.Tamer.CurrentCondition).Serialize());

                        return;
                    }
                }

                // Verification of items amount more than he have in bag
                var HasQuanty = client.Tamer.Inventory.CountItensById(item.ItemId);
                
                if (item.Amount > HasQuanty)
                {
                    //client.Send(new DisconnectUserPacket($"You not have {item.Amount}x {item.ItemInfo.Name}!").Serialize());
                    _logger.Warning($"Tamer {client.Tamer.Name} tryed to sell more itens than he have !!");
                    client.Send(new SystemMessagePacket($"You don't have {item.Amount}x of {item.ItemInfo.Name}!\n ShopClosed !!"));

                    client.Tamer.UpdateCurrentCondition(ConditionEnum.Default);

                    client.Tamer.Inventory.AddItems(client.Tamer.TamerShop.Items);
                    client.Tamer.TamerShop.Clear();

                    client.Send(new PersonalShopPacket());

                    await _sender.Send(new UpdateItemsCommand(client.Tamer.Inventory));
                    await _sender.Send(new UpdateItemsCommand(client.Tamer.TamerShop));

                    client.Send(new LoadInventoryPacket(client.Tamer.Inventory, InventoryTypeEnum.Inventory).Serialize());

                    _mapServer.BroadcastForTamerViewsAndSelf(client.TamerId, new SyncConditionPacket(client.Tamer.GeneralHandler, client.Tamer.CurrentCondition).Serialize());

                    return;
                }

                _logger.Debug($"{item.ItemId} {item.Amount} {item.TamerShopSellPrice}");
            }

            //_logger.Information($"TamerShop Items Amount: {client.Tamer.TamerShop.Count}\n");

            _logger.Verbose($"Updating tamer shop item list...");
            client.Tamer.TamerShop.AddItems(sellList.Clone(), true);
            await _sender.Send(new UpdateItemsCommand(client.Tamer.TamerShop));

            _logger.Verbose($"Updating tamer inventory item list...");
            client.Tamer.Inventory.RemoveOrReduceItems(sellList.Clone());
            await _sender.Send(new UpdateItemsCommand(client.Tamer.Inventory));

            client.Tamer.UpdateCurrentCondition(ConditionEnum.TamerShop);
            client.Tamer.UpdateShopName(shopName);

            _logger.Verbose($"Sending sync in condition packet...");
            _mapServer.BroadcastForTamerViewsAndSelf(client.TamerId, new SyncConditionPacket(client.Tamer.GeneralHandler, client.Tamer.CurrentCondition, shopName).Serialize());

            _logger.Verbose($"Sending tamer shop view packet...");
            client.Send(new PersonalShopItemsViewPacket(client.Tamer.TamerShop, client.Tamer.ShopName));

            //_logger.Information($"ShopName: {client.Tamer.ShopName}, Items Amount: {client.Tamer.TamerShop.Count}\n");

            _logger.Verbose($"Sending tamer shop open packet...");
            client.Send(new PersonalShopPacket(client.Tamer.ShopItemId));
            client.Send(new LoadInventoryPacket(client.Tamer.Inventory, InventoryTypeEnum.Inventory).Serialize());

            await _sender.Send(new UpdateItemListBitsCommand(client.Tamer.Inventory.Id, client.Tamer.Inventory.Bits));
        }

    }
}