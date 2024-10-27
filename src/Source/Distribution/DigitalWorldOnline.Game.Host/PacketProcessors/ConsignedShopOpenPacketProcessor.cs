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

            _logger.Verbose($"ConsigmentShop Open Packet 1516");

            var posX = packet.ReadInt();
            var posY = packet.ReadInt();
            packet.Skip(4);
            var shopName = packet.ReadString();
            packet.Skip(9);
            var sellQuantity = packet.ReadInt();

            _logger.Verbose($"Shop Location: Map {client.Tamer.Location.MapId} ({posX}, {posY}), ShopName: {shopName}, Items Amount: {sellQuantity}\n");

            List<ItemModel> sellList = new(sellQuantity);

            for (int i = 0; i < sellQuantity; i++)
            {
                var sellItem = new ItemModel(packet.ReadInt(), packet.ReadInt());

                packet.Skip(64);

                var price = packet.ReadInt64();
                sellItem.SetSellPrice(price);

                packet.Skip(12);
                sellList.Add(sellItem);

                //_logger.Information($"SellItem index: {i} | SellItem: {sellItem.ItemId}");
                //_logger.Information($"SellItem Amount: {sellItem.Amount} | SellItem Price: {sellItem.TamerShopSellPrice}");
            }

            foreach (var item in sellList)
            {
                item.SetItemInfo(_assets.ItemInfo.First(x => x.ItemId == item.ItemId));

                foreach (var item2 in sellList)
                {
                    if (item2.ItemId == item.ItemId && item2.TamerShopSellPrice != item.TamerShopSellPrice)
                    {
                        _logger.Error($"Tamer {client.Tamer.Name} tryed to add 2 items of same id with different price!");
                        client.Send(new DisconnectUserPacket("You cant add 2 items of same id with different price!").Serialize());
                        return;
                    }
                }

                var HasQuanty = client.Tamer.Inventory.CountItensById(item.ItemId);

                if (item.Amount > HasQuanty)
                {
                    _logger.Error($"You not have {item.Amount}x {item.ItemInfo.Name}!");
                    client.Send(new DisconnectUserPacket($"You not have {item.Amount}x {item.ItemInfo.Name}!").Serialize());
                    return;
                }
                
            }

            _logger.Verbose($"Updating consigned shop item list...");
            client.Tamer.ConsignedShopItems.AddItems(sellList.Clone());
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