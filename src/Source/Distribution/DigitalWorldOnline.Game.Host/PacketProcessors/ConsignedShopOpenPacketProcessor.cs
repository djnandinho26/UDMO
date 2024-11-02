using DigitalWorldOnline.Application;
using DigitalWorldOnline.Application.Separar.Commands.Create;
using DigitalWorldOnline.Application.Separar.Commands.Update;
using DigitalWorldOnline.Commons.Entities;
using DigitalWorldOnline.Commons.Enums.ClientEnums;
using DigitalWorldOnline.Commons.Enums.PacketProcessor;
using DigitalWorldOnline.Commons.Extensions;
using DigitalWorldOnline.Commons.Interfaces;
using DigitalWorldOnline.Commons.Models.Base;
using DigitalWorldOnline.Commons.Models.TamerShop;
using DigitalWorldOnline.Commons.Packets.GameServer;
using DigitalWorldOnline.Commons.Packets.PersonalShop;
using DigitalWorldOnline.GameHost;
using MediatR;
using Serilog;

namespace DigitalWorldOnline.Game.PacketProcessors
{
    public class ConsignedShopOpenPacketProcessor : IGamePacketProcessor
    {
        public GameServerPacketEnum Type => GameServerPacketEnum.ConsignedShopOpen;

        private readonly AssetsLoader _assets;
        private readonly MapServer _mapServer;
        private readonly ILogger _logger;
        private readonly ISender _sender;

        public ConsignedShopOpenPacketProcessor(
            MapServer mapServer,
            AssetsLoader assets,
            ILogger logger,
            ISender sender)
        {
            _mapServer = mapServer;
            _assets = assets;
            _logger = logger;
            _sender = sender;
        }

        public async Task Process(GameClient client, byte[] packetData)
        {
            var packet = new GamePacketReader(packetData);

            _logger.Verbose($"--- ConsigmentShop Open Packet 1516 ---");

            var posX = packet.ReadInt();
            var posY = packet.ReadInt();
            packet.Skip(4);
            var shopName = packet.ReadString();
            packet.Skip(9);
            var sellQuantity = packet.ReadInt();

            _logger.Verbose($"Shop Location: Map {client.Tamer.Location.MapId} ({posX}, {posY}), ShopName: {shopName}, Items Amount: {sellQuantity}\n");

            List<ItemModel> sellList = new(sellQuantity);


            //_logger.Information($"-------------------------------------\n");

            for (int i = 0; i < sellQuantity; i++)
            {
                var itemId = packet.ReadInt();
                var itemAmount = packet.ReadInt();

                Console.WriteLine($"Item Index: {i} | ItemId: {itemId} | ItemAmount: {itemAmount}");

                /*var existingItem = sellList.FirstOrDefault(x => x.ItemId == itemId);

                if (existingItem != null)
                {
                    // Incrementa a quantidade no slot existente
                    existingItem.Amount += itemAmount;
                    Console.WriteLine($"Item {itemId} já está na lista, aumentando quantidade para {existingItem.Amount}");

                    packet.Skip(64);

                    var price = packet.ReadInt64();
                    existingItem.SetSellPrice(price); // Atualiza o preço do item existente

                    packet.Skip(8);
                }
                else
                {*/
                    // Adiciona um novo item à lista
                    var sellItem = new ItemModel(itemId, itemAmount);

                    packet.Skip(64);

                    var price = packet.ReadInt64();
                    sellItem.SetSellPrice(price);

                    packet.Skip(8);
                    sellList.Add(sellItem);

                    Console.WriteLine($"Item Index: {i} | Price: {price}\n");
                //}
            }

            //sellList =  sellList.GroupBy(x => x.ItemId)
            //    .Select(item =>
            //        {
            //            var firstItem = item.First();
            //            firstItem.Amount = item.Sum(p => p.Amount); // Set Amount to sum of ages in the group
            //            return firstItem;
            //        })
            //    .ToList();
            _logger.Information($"sell items count: {sellList.Count}");

            //_logger.Information($"-------------------------------------");

            foreach (var item in sellList)
            {
                _logger.Information($"item: {item.ItemId} and amount: {item.Amount}");
                item.SetItemInfo(_assets.ItemInfo.First(x => x.ItemId == item.ItemId));

                var itemsCount = sellList.Count(x => x.ItemId == item.ItemId && x.TamerShopSellPrice != item.TamerShopSellPrice);

                if(itemsCount > 0)
                {
                    _logger.Error($"Tamer {client.Tamer.Name} tryed to add 2 items of same id with different price!");
                    client.Send(new DisconnectUserPacket("You cant add 2 items of same id with different price!").Serialize());
                    return;
                }

                //foreach (var item2 in sellList)
                //{
                //    if (item2.ItemId == item.ItemId && item2.TamerShopSellPrice != item.TamerShopSellPrice)
                //    {
                //        _logger.Error($"Tamer {client.Tamer.Name} tryed to add 2 items of same id with different price!");
                //        client.Send(new DisconnectUserPacket("You cant add 2 items of same id with different price!").Serialize());
                //        return;
                //    }
                //}

                var HasQuanty = client.Tamer.Inventory.CountItensById(item.ItemId);

                if (item.Amount > HasQuanty)
                {
                    _logger.Error($"You not have {item.Amount}x {item.ItemInfo.Name}!");
                    client.Send(new DisconnectUserPacket($"You not have {item.Amount}x {item.ItemInfo.Name}!").Serialize());
                    return;
                }

            }

            _logger.Verbose($"Updating consigned shop item list...");
            client.Tamer.ConsignedShopItems.AddItems(sellList.Clone(), true);
            await _sender.Send(new UpdateItemsCommand(client.Tamer.ConsignedShopItems));

            _logger.Verbose($"Updating tamer inventory item list...");
            client.Tamer.Inventory.RemoveOrReduceItems(sellList.Clone());
            await _sender.Send(new UpdateItemsCommand(client.Tamer.Inventory));

            _logger.Verbose($"Creating consigned shop...");
            var newShop = ConsignedShop.Create(client.TamerId, shopName, posX, posY, client.Tamer.Location.MapId, client.Tamer.Channel, client.Tamer.ShopItemId);

            var Id = await _sender.Send(new CreateConsignedShopCommand(newShop));

            newShop.SetId(Id.Id);
            newShop.SetGeneralHandler(Id.GeneralHandler);

            _logger.Verbose($"Sending consigned shop load packet...");
            _mapServer.BroadcastForTamerViewsAndSelf(client.TamerId, new LoadConsignedShopPacket(newShop).Serialize());

            _logger.Verbose($"Sending personal shop close packet...");
            client.Tamer.UpdateShopItemId(0);
            client.Send(new PersonalShopPacket(TamerShopActionEnum.CloseWindow, client.Tamer.ShopItemId));
            client.Tamer.RestorePreviousCondition();

            _logger.Verbose($"Sending sync in condition packet...");
            _mapServer.BroadcastForTamerViewsAndSelf(client.TamerId, new SyncConditionPacket(client.Tamer.GeneralHandler, client.Tamer.CurrentCondition).Serialize());
        }
    }
}