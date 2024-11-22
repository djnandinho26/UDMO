using AutoMapper;
using DigitalWorldOnline.Application.Separar.Commands.Update;
using DigitalWorldOnline.Application.Separar.Queries;
using DigitalWorldOnline.Commons.Entities;
using DigitalWorldOnline.Commons.Enums.Character;
using DigitalWorldOnline.Commons.Enums.ClientEnums;
using DigitalWorldOnline.Commons.Enums.PacketProcessor;
using DigitalWorldOnline.Commons.Interfaces;
using DigitalWorldOnline.Commons.Models.Asset;
using DigitalWorldOnline.Commons.Models.Mechanics;
using DigitalWorldOnline.Commons.Packets.Chat;
using DigitalWorldOnline.Commons.Packets.GameServer;
using DigitalWorldOnline.Commons.Packets.MapServer;
using DigitalWorldOnline.Commons.Utils;
using DigitalWorldOnline.Game.Managers;
using DigitalWorldOnline.GameHost;
using MediatR;
using Microsoft.Extensions.Configuration;

namespace DigitalWorldOnline.Game.PacketProcessors
{
    public class DieConfirmPacketProcessor : IGamePacketProcessor
    {
        public GameServerPacketEnum Type => GameServerPacketEnum.DieConfirm;

        private const string GameServerAddress = "GameServer:Address";
        private const string GamerServerPublic = "GameServer:PublicAddress";
        private const string GameServerPort = "GameServer:Port";

        private readonly PartyManager _partyManager;
        private readonly MapServer _mapServer;
        private readonly ISender _sender;
        private readonly IMapper _mapper;
        private readonly IConfiguration _configuration;
        private readonly DungeonsServer _dungeonServer;

        public DieConfirmPacketProcessor(
            PartyManager partyManager,
            MapServer mapServer,
            ISender sender,
            IMapper mapper,
            IConfiguration configuration,
            DungeonsServer dungeonsServer)
        {
            _partyManager = partyManager;
            _mapServer = mapServer;
            _sender = sender;
            _mapper = mapper;
            _configuration = configuration;
            _dungeonServer = dungeonsServer;
        }

        public async Task Process(GameClient client, byte[] packetData)
        {
            if (client.DungeonMap)
            {
                client.Tamer.Die();
                
                GameParty? party = _partyManager.FindParty(client.TamerId);

                var map = UtilitiesFunctions.MapGroup(client.Tamer.Location.MapId);
                bool shouldReviveInSameMap = false;
                if (party != null && party.Members.Values.Count(x =>
                        x.Id != client.TamerId && x.Location.MapId == client.Tamer.Location.MapId &&
                        x.Channel == client.Tamer.Channel && client.Tamer.Alive) > 0
                   )
                {
                    map = client.Tamer.Location.MapId;
                    shouldReviveInSameMap = true;
                }

                var mapConfig = await _sender.Send(new GameMapConfigByMapIdQuery(map));
                var waypoints = await _sender.Send(new MapRegionListAssetsByMapIdQuery(map));

                if (mapConfig == null || waypoints == null || !waypoints.Regions.Any())
                {
                    client.Send(new SystemMessagePacket($"Map information not found for map Id {map}."));
                    return;
                }

                var destination = waypoints.Regions.First();


                // client.Tamer.Partner.CurrentEvoluti
                if (shouldReviveInSameMap)
                {
                    client.Tamer.UpdateCurrentCondition(ConditionEnum.Default);
                    client.Tamer.UpdateState(CharacterStateEnum.Ready);
                    client.Tamer.Revive();
                    client.Send(new UpdateStatusPacket(client.Tamer));
                    client.Send(new SyncConditionPacket(client.Tamer.GeneralHandler, client.Tamer.CurrentCondition));
                    client.Send(new SyncConditionPacket(client.Partner.GeneralHandler,
                        client.Partner.CurrentCondition));
                }

                await _sender.Send(new UpdateCharacterBasicInfoCommand(client.Tamer));
                await _sender.Send(new UpdateCharacterActiveEvolutionCommand(client.Tamer.ActiveEvolution));

                client.Tamer.NewLocation(map, destination.X, destination.Y);
                await _sender.Send(new UpdateCharacterLocationCommand(client.Tamer.Location));

                client.Tamer.Partner.NewLocation(map, destination.X, destination.Y);
                await _sender.Send(new UpdateDigimonLocationCommand(client.Tamer.Partner.Location));


                if (shouldReviveInSameMap)
                {
                    _dungeonServer.BroadcastForTamerViewsAndSelf(client.TamerId,
                        new LocalMapSwapPacket(client.Tamer.GeneralHandler, client.Tamer.Partner.GeneralHandler,
                            client.Tamer.Location.X, client.Tamer.Location.Y, client.Tamer.Location.X,
                            client.Tamer.Location.Y).Serialize());
                }
                else
                {
                    _dungeonServer.RemoveClient(client);
                    client.Send(new MapSwapPacket(
                            _configuration[GamerServerPublic],
                            _configuration[GameServerPort],
                            client.Tamer.Location.MapId,
                            client.Tamer.Location.X,
                            client.Tamer.Location.Y)
                        .Serialize());
                }
            }
            else
            {
                var destiny =
                    _mapper.Map<MapRegionListAssetModel>(
                        await _sender.Send(new MapRegionListAssetsByMapIdQuery(client.Tamer.Location.MapId)));
                var region = destiny?.Regions.FirstOrDefault();

                if (region != null)
                {
                    client.Tamer.NewLocation(region.X, region.Y);
                    await _sender.Send(new UpdateCharacterLocationCommand(client.Tamer.Location));

                    client.Tamer.Partner.NewLocation(region.X, region.Y);
                    await _sender.Send(new UpdateDigimonLocationCommand(client.Tamer.Partner.Location));
                }

                client.Tamer.UpdateState(CharacterStateEnum.Loading);
                await _sender.Send(new UpdateCharacterStateCommand(client.TamerId, CharacterStateEnum.Loading));

                _mapServer.RemoveClient(client);

                client.Send(new MapSwapPacket(
                        _configuration[GamerServerPublic],
                        _configuration[GameServerPort],
                        client.Tamer.Location.MapId,
                        client.Tamer.Location.X,
                        client.Tamer.Location.Y)
                    .Serialize());
            }
        }
    }
}