using DigitalWorldOnline.Commons.Entities;
using DigitalWorldOnline.Commons.Enums.Account;
using DigitalWorldOnline.Commons.Enums.Character;
using DigitalWorldOnline.Commons.Enums.ClientEnums;
using DigitalWorldOnline.Commons.Enums.PacketProcessor;
using DigitalWorldOnline.Commons.Interfaces;
using DigitalWorldOnline.Commons.Models.Base;
using DigitalWorldOnline.Commons.Packets.Chat;
using DigitalWorldOnline.Commons.Packets.GameServer;
using DigitalWorldOnline.GameHost;
using Serilog;
using System.Net.Sockets;

namespace DigitalWorldOnline.Game.PacketProcessors
{
    public class TradeAddItemPacketProcessor : IGamePacketProcessor
    {
        public GameServerPacketEnum Type => GameServerPacketEnum.TradeAddItem;

        private readonly MapServer _mapServer;
        private readonly ILogger _logger;


        public TradeAddItemPacketProcessor(
            MapServer mapServer,
            ILogger logger)
        {
            _mapServer = mapServer;
            _logger = logger;

        }

        public async Task Process(GameClient client, byte[] packetData)
        {
            var packet = new GamePacketReader(packetData);
            var inventorySlot = packet.ReadShort();
            var amount = packet.ReadShort();
            var slotAtual = client.Tamer.TradeInventory.EquippedItems.Count;

            var targetClient = _mapServer.FindClientByTamerHandleAndChannel(client.Tamer.TargetTradeGeneralHandle, client.TamerId);

            var Item = client.Tamer.Inventory.FindItemBySlot(inventorySlot);

            // Verificação de quantidade suficiente no inventário
            if (client.Tamer.Inventory.CountItensById(Item.ItemId) < amount)
            {
                //sistema de banimento permanente
                var banProcessor = new BanForCheating();
                var banMessage = banProcessor.BanAccountWithMessage(client.AccountId, client.Tamer.Name, AccountBlockEnum.Permannent, "Cheating");

                var chatPacket = new NoticeMessagePacket(banMessage);
                client.Send(new DisconnectUserPacket($"YOU HAVE BEEN PERMANENTLY BANNED").Serialize());
                _logger.Error($"[DISCONNECTED] {client.Tamer.Name} tried DUPPING in TRADE {amount}x {Item.ItemInfo.Name}, but only has {Item.Amount}x!");

                client.Disconnect();
                return;
            }

            // Verificar se o item já foi adicionado para evitar duplicação
            if (client.Tamer.TradeInventory.EquippedItems.Any(i => i.ItemId == Item.ItemId))
            {
                _logger.Warning($"[WARNING] {client.Tamer.Name} attempted to add duplicate item {Item.ItemInfo.Name} in trade.");
                return;
            }

            // Seleciona slot vazio para o novo item
            var EmptSlot = client.Tamer.TradeInventory.GetEmptySlot;
            if (EmptSlot == -1)
            {
                client.Send(new ChatMessagePacket("No empty slot available in trade inventory.", ChatTypeEnum.Notice, "System"));
                return;
            }

            // Clona e adiciona o item na troca
            var NewItem = (ItemModel)Item.Clone();
            NewItem.Amount = amount;
            client.Tamer.TradeInventory.AddItemTrade(NewItem);

            // Envia pacotes de atualização para ambos os clientes
            client.Send(new TradeAddItemPacket(client.Tamer.GeneralHandler, NewItem.ToArray(), (byte)EmptSlot, inventorySlot));
            targetClient.Send(new TradeAddItemPacket(client.Tamer.GeneralHandler, NewItem.ToArray(), (byte)EmptSlot, inventorySlot));

            // Bloqueia o inventário até confirmação da troca
            targetClient.Send(new TradeInventoryUnlockPacket(client.Tamer.TargetTradeGeneralHandle));
            client.Send(new TradeInventoryUnlockPacket(client.Tamer.TargetTradeGeneralHandle));
        }

    }
}

