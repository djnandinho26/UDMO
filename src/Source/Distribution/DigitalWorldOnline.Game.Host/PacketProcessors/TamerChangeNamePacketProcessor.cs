using DigitalWorldOnline.Application.Separar.Commands.Update;
using DigitalWorldOnline.Application.Separar.Queries;
using DigitalWorldOnline.Commons.Entities;
using DigitalWorldOnline.Commons.Enums.Character;
using DigitalWorldOnline.Commons.Enums.PacketProcessor;
using DigitalWorldOnline.Commons.Interfaces;
using DigitalWorldOnline.Commons.Packets.GameServer;
using DigitalWorldOnline.GameHost;
using MediatR;
using Serilog;
using DigitalWorldOnline.Commons.Models.Map;
using DigitalWorldOnline.Commons.Packets.MapServer;
using DigitalWorldOnline.Commons.Utils;

namespace DigitalWorldOnline.Game.PacketProcessors
{
    public class TamerChangeNamePacketProcessor : IGamePacketProcessor
    {
        public GameServerPacketEnum Type => GameServerPacketEnum.TamerChangeName;

        private readonly MapServer _mapServer;
        private readonly DungeonsServer _dungeonServer;
        private readonly ILogger _logger;
        private readonly ISender _sender;

        public TamerChangeNamePacketProcessor(
            MapServer mapServer,
            DungeonsServer dungeonServer,
            ILogger logger,
            ISender sender)
        {
            _mapServer = mapServer;
            _dungeonServer = dungeonServer;
            _logger = logger;
            _sender = sender;
        }

        public async Task Process(GameClient client, byte[] packetData)
        {
            var packet = new GamePacketReader(packetData);

            int itemSlot = packet.ReadInt();
            var newName = packet.ReadString();
            var oldName = client.Tamer.Name;
            var AvaliabeName = await _sender.Send(new CharacterByNameQuery(newName)) == null;

            if (!AvaliabeName)
            {
                client.Send(new TamerChangeNamePacket(CharacterChangeNameType.Existing, oldName, newName, itemSlot));
                return;
            }

            var inventoryItem = client.Tamer.Inventory.FindItemBySlot(itemSlot);

            if (inventoryItem != null)
            {
                client.Tamer.Inventory.RemoveOrReduceItem(inventoryItem, 1, itemSlot);
                client.Tamer.UpdateName(newName);

                await _sender.Send(new ChangeTamerNameByIdCommand(client.Tamer.Id, newName));
                await _sender.Send(new UpdateItemsCommand(client.Tamer.Inventory));

                client.Send(new TamerChangeNamePacket(CharacterChangeNameType.Sucess, itemSlot, oldName, newName));
                client.Send(new TamerChangeNamePacket(CharacterChangeNameType.Complete, newName, newName, itemSlot));
                
                _mapServer.BroadcastForTamerViews(client, UtilitiesFunctions.GroupPackets(
                    new UnloadTamerPacket(client.Tamer).Serialize(),
                    new LoadTamerPacket(client.Tamer).Serialize()
                ));
                _dungeonServer.BroadcastForTamerViews(client.TamerId, UtilitiesFunctions.GroupPackets(
                    new UnloadTamerPacket(client.Tamer).Serialize(),
                    new LoadTamerPacket(client.Tamer).Serialize()
                ));
                
                List<long> friendsIds = client.Tamer.Friended.Select(x => x.CharacterId).ToList();

                _mapServer.BroadcastForTargetTamers(friendsIds,
                    new FriendChangeNamePacket(oldName, newName, false).Serialize());
                _dungeonServer.BroadcastForTargetTamers(friendsIds,
                    new FriendChangeNamePacket(oldName, newName, false).Serialize());

                List<long> foesIds = client.Tamer.Foed.Select(x => x.CharacterId).ToList();

                _mapServer.BroadcastForTargetTamers(foesIds,
                    new FriendChangeNamePacket(oldName, newName, true).Serialize());
                _dungeonServer.BroadcastForTargetTamers(foesIds,
                    new FriendChangeNamePacket(oldName, newName, true).Serialize());

                _logger.Verbose($"Character {client.TamerId} Changed Name {oldName} to {newName}.");
            }
        }
    }
}