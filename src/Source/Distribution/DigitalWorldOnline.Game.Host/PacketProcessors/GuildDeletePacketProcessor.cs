using DigitalWorldOnline.Application.Separar.Commands.Delete;
using DigitalWorldOnline.Commons.Entities;
using DigitalWorldOnline.Commons.Enums.ClientEnums;
using DigitalWorldOnline.Commons.Enums.PacketProcessor;
using DigitalWorldOnline.Commons.Interfaces;
using DigitalWorldOnline.Commons.Models.Mechanics;
using DigitalWorldOnline.Commons.Packets.GameServer;
using DigitalWorldOnline.Commons.Packets.MapServer;
using DigitalWorldOnline.GameHost;
using MediatR;
using Serilog;
using AutoMapper;

namespace DigitalWorldOnline.Game.PacketProcessors
{
    public class GuildDeletePacketProcessor : IGamePacketProcessor
    {
        public GameServerPacketEnum Type => GameServerPacketEnum.GuildDelete;

        private readonly MapServer _mapServer;
        private readonly DungeonsServer _dungeonServer;
        private readonly ILogger _logger;
        private readonly ISender _sender;
        private readonly IMapper _mapper;

        public GuildDeletePacketProcessor(MapServer mapServer, DungeonsServer dungeonServer, ILogger logger, ISender sender, IMapper mapper)
        {
            _mapServer = mapServer;
            _dungeonServer = dungeonServer;
            _logger = logger;
            _sender = sender;
            _mapper = mapper;
        }

        public async Task Process(GameClient client, byte[] packetData)
        {
            _logger.Debug($"GuildDelete Packet 2102");

            if (client.Tamer.Guild != null)
            {
                var guild = client.Tamer.Guild;

                _logger.Debug($"Deleting guild {guild.Id} : {guild.Name}");

                client.Send(new GuildDeletePacket(guild.Name));

                try
                {
                    await _sender.Send(new DeleteGuildCommand(guild.Id));

                    client.Tamer.SetGuild();
                }
                catch (Exception ex)
                {
                    _logger.Error($"ErrorGuild:\n{ex.Message}");
                }

                if (client.DungeonMap)
                {
                    _dungeonServer.BroadcastForTargetTamers(client.TamerId, new UnloadTamerPacket(client.Tamer).Serialize());
                    _dungeonServer.BroadcastForTargetTamers(client.TamerId, new LoadTamerPacket(client.Tamer).Serialize());
                    _dungeonServer.BroadcastForTargetTamers(client.TamerId, new LoadBuffsPacket(client.Tamer).Serialize());
                }
                else
                {
                    _mapServer.BroadcastForTargetTamers(client.TamerId, new UnloadTamerPacket(client.Tamer).Serialize());
                    _mapServer.BroadcastForTargetTamers(client.TamerId, new LoadTamerPacket(client.Tamer).Serialize());
                    _mapServer.BroadcastForTargetTamers(client.TamerId, new LoadBuffsPacket(client.Tamer).Serialize());
                }

                _logger.Debug($"Tamer {client.Tamer.Name} deleted guild {guild.Id} : {guild.Name}.");
            }
            else
            {
                _logger.Debug($"Tamer {client.Tamer.Name} not in guild.");
            }
        }
    }
}