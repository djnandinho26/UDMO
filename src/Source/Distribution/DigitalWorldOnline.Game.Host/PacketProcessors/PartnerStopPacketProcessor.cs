using DigitalWorldOnline.Commons.Entities;
using DigitalWorldOnline.Commons.Enums.PacketProcessor;
using DigitalWorldOnline.Commons.Interfaces;
using DigitalWorldOnline.Commons.Packets.GameServer.Combat;
using DigitalWorldOnline.GameHost;
using DigitalWorldOnline.GameHost.EventsServer;
using Serilog;

namespace DigitalWorldOnline.Game.PacketProcessors
{
    public class PartnerStopPacketProcessor : IGamePacketProcessor
    {
        public GameServerPacketEnum Type => GameServerPacketEnum.PartnerStop;

        private readonly MapServer _mapServer;
        private readonly DungeonsServer _dungeonServer;
        private readonly EventServer _eventServer;
        private readonly PvpServer _pvpServer;
        private readonly ILogger _logger;

        public PartnerStopPacketProcessor(MapServer mapServer, DungeonsServer dungeonsServer, EventServer eventServer, PvpServer pvpServer, ILogger logger)
        {
            _mapServer = mapServer;
            _dungeonServer = dungeonsServer;
            _eventServer = eventServer;
            _pvpServer = pvpServer;
            _logger = logger;
        }

        public Task Process(GameClient client, byte[] packetData)
        {
            client.Tamer.Partner.StopAutoAttack();

            if (client.DungeonMap)
            {
                _dungeonServer.BroadcastForTamerViewsAndSelf(client.TamerId, new PartnerStopPacket(client.Tamer.Partner.GeneralHandler).Serialize());
            }
            else if (client.PvpMap)
            {
                _pvpServer.BroadcastForTamerViewsAndSelf(client.TamerId, new PartnerStopPacket(client.Tamer.Partner.GeneralHandler).Serialize());
            }
            else
            {
                _mapServer.BroadcastForTamerViewsAndSelf(client.TamerId, new PartnerStopPacket(client.Tamer.Partner.GeneralHandler).Serialize());
                _eventServer.BroadcastForTamerViewsAndSelf(client.TamerId, new PartnerStopPacket(client.Tamer.Partner.GeneralHandler).Serialize());
            }

            return Task.CompletedTask;
        }
    }
}