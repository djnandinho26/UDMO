using DigitalWorldOnline.Application;
using DigitalWorldOnline.Application.Separar.Commands.Update;
using DigitalWorldOnline.Commons.Entities;
using DigitalWorldOnline.Commons.Enums.ClientEnums;
using DigitalWorldOnline.Commons.Enums.PacketProcessor;
using DigitalWorldOnline.Commons.Interfaces;
using DigitalWorldOnline.Commons.Models.Base;
using DigitalWorldOnline.Commons.Packets.Chat;
using DigitalWorldOnline.Commons.Packets.GameServer;
using DigitalWorldOnline.Commons.Packets.Items;
using DigitalWorldOnline.Commons.Writers;
using MediatR;
using Serilog;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

namespace DigitalWorldOnline.Game.PacketProcessors
{
    public class EncyclopediaGetRewardPacketProcessor : IGamePacketProcessor
    {
        public GameServerPacketEnum Type => GameServerPacketEnum.EncyclopediaGetReward;

        private readonly AssetsLoader _assets;
        private readonly ISender _sender;
        private readonly ILogger _logger;

        public EncyclopediaGetRewardPacketProcessor(AssetsLoader assets, ISender sender,ILogger logger)
        {
            _assets = assets;
            _sender = sender;
            _logger = logger;
        }

        public async Task Process(GameClient client, byte[] packetData)
        {
            var packet = new GamePacketReader(packetData);
            
            // Get digimon id
            var digimonId = packet.ReadUInt();

            var encyclopedia = client.Tamer.Encyclopedia;

            var itemId = 97206;
            var newItem = new ItemModel();
            newItem.SetItemInfo(_assets.ItemInfo.FirstOrDefault(x => x.ItemId == itemId));

            if (newItem.ItemInfo == null)
            {
                _logger.Warning($"Failed to send encyclopedia reward to tamer {client.TamerId}.");
                client.Send(new SystemMessagePacket($"Failed to receive reward."));
                return;
            }

            newItem.ItemId = itemId;
            newItem.Amount = 10;

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
            }

            client.Send(new EncyclopediaLoadPacketPacket(encyclopedia));
        }
    }
}