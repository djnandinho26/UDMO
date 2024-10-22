using DigitalWorldOnline.Application.Separar.Commands.Update;
using DigitalWorldOnline.Application.Separar.Queries;
using DigitalWorldOnline.Commons.Entities;
using DigitalWorldOnline.Commons.Enums.Character;
using DigitalWorldOnline.Commons.Enums.PacketProcessor;
using DigitalWorldOnline.Commons.Interfaces;
using DigitalWorldOnline.Commons.Packets.Chat;
using DigitalWorldOnline.Commons.Packets.GameServer;
using DigitalWorldOnline.Commons.Packets.MapServer;
using DigitalWorldOnline.Commons.Utils;
using DigitalWorldOnline.Game.Managers;
using DigitalWorldOnline.GameHost;
using MediatR;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace DigitalWorldOnline.Game.PacketProcessors
{
    public class PartyMemberLeavePacketProcessor : IGamePacketProcessor
    {
        public GameServerPacketEnum Type => GameServerPacketEnum.PartyMemberLeave;

        private const string GameServerAddress = "GameServer:Address";
        private const string GamerServerPublic = "GameServer:PublicAddress";
        private const string GameServerPort = "GameServer:Port";


        private readonly PartyManager _partyManager;
        private readonly MapServer _mapServer;
        private readonly DungeonsServer _dungeonServer;
        private readonly ILogger _logger;
        private readonly ISender _sender;
        private readonly IConfiguration _configuration;

        public PartyMemberLeavePacketProcessor(PartyManager partyManager, MapServer mapServer,
            ILogger logger, ISender sender, IConfiguration configuration,
            DungeonsServer dungeonServer)
        {
            _partyManager = partyManager;
            _mapServer = mapServer;
            _logger = logger;
            _sender = sender;
            _configuration = configuration;
            _dungeonServer = dungeonServer;
        }
        public async Task Process(GameClient client, byte[] packetData)
        {
            var packet = new GamePacketReader(packetData);

            var targetName = packet.ReadString();

            var party = _partyManager.FindParty(client.TamerId);

            if (party != null)
            {
                try
                {
                    var membersList = party.GetMembersIdList();
                    var leaveTargetKey = party[client.TamerId].Key;

                    if (party.LeaderId == party[client.TamerId].Key && party.Members.Count > 2)
                    {
                        party.RemoveMember(party[client.TamerId].Key);

                        var randomIndex = new Random().Next(party.Members.Count);
                        var sortedPlayer = party.Members.ElementAt(randomIndex).Key;

                        party.ChangeLeader(sortedPlayer);

                        _mapServer.BroadcastForTargetTamers(party.GetMembersIdList(), new PartyLeaderChangedPacket(sortedPlayer).Serialize());
                        _dungeonServer.BroadcastForTargetTamers(party.GetMembersIdList(), new PartyLeaderChangedPacket(sortedPlayer).Serialize());
                    }
                    else if (party.Members.Count <= 2)
                    {
                        //_logger.Information($"{client.Tamer.Name} left the party !!");

                        foreach (var target in party.Members.Values)
                        {
                            var dungeonClient = _dungeonServer.FindClientByTamerId(target.Id);

                            if (dungeonClient == null)
                            {
                                party.RemoveMember(leaveTargetKey);
                                _mapServer.BroadcastForTargetTamers(membersList, new PartyMemberLeavePacket(leaveTargetKey).Serialize());
                                continue;
                            }

                            // -- Teleport player outside of Dungeon ---------------------------------
                            var map = UtilitiesFunctions.MapGroup(dungeonClient.Tamer.Location.MapId);

                            var mapConfig = await _sender.Send(new GameMapConfigByMapIdQuery(dungeonClient.Tamer.Location.MapId));
                            var waypoints = await _sender.Send(new MapRegionListAssetsByMapIdQuery(map));

                            if (mapConfig == null || waypoints == null || !waypoints.Regions.Any())
                            {
                                client.Send(new SystemMessagePacket($"Map information not found for map Id {map}."));
                                _logger.Warning($"Map information not found for map Id {map} on character {client.TamerId} jump booster.");
                                return;
                            }

                            var mapRegionIndex = mapConfig.MapRegionindex;
                            var destination = waypoints.Regions.FirstOrDefault(x => x.Index == mapRegionIndex);

                            _dungeonServer.RemoveClient(dungeonClient);

                            dungeonClient.Tamer.NewLocation(map, destination.X, destination.Y);
                            await _sender.Send(new UpdateCharacterLocationCommand(dungeonClient.Tamer.Location));

                            dungeonClient.Tamer.Partner.NewLocation(map, destination.X, destination.Y);
                            await _sender.Send(new UpdateDigimonLocationCommand(dungeonClient.Tamer.Partner.Location));

                            dungeonClient.Tamer.UpdateState(CharacterStateEnum.Loading);
                            await _sender.Send(new UpdateCharacterStateCommand(dungeonClient.TamerId, CharacterStateEnum.Loading));

                            dungeonClient.SetGameQuit(false);

                            dungeonClient.Send(new MapSwapPacket(_configuration[GamerServerPublic], _configuration[GameServerPort],
                                dungeonClient.Tamer.Location.MapId, dungeonClient.Tamer.Location.X, dungeonClient.Tamer.Location.Y));

                            party.RemoveMember(leaveTargetKey);
                            _dungeonServer.BroadcastForTargetTamers(membersList, new PartyMemberLeavePacket(party[client.TamerId].Key).Serialize());
                        }

                        _partyManager.RemoveParty(party.Id);
                    }
                    else
                    {
                        //_logger.Information($"{client.Tamer.Name} left the party !!");

                        var partyMember = party[client.TamerId].Value;

                        var leaveClient = _mapServer.FindClientByTamerId(partyMember.Id);

                        if (leaveClient == null) leaveClient = _dungeonServer.FindClientByTamerId(partyMember.Id);

                        if (leaveClient != null) leaveClient.Send(new PartyMemberLeavePacket(leaveTargetKey).Serialize());

                        party.RemoveMember(leaveTargetKey);

                        // -------------------------------------------------------

                        var dungeonClient = _dungeonServer.FindClientByTamerId(partyMember.Id);

                        if (dungeonClient != null)
                        {
                            var map = UtilitiesFunctions.MapGroup(dungeonClient.Tamer.Location.MapId);

                            var mapConfig = await _sender.Send(new GameMapConfigByMapIdQuery(dungeonClient.Tamer.Location.MapId));
                            var waypoints = await _sender.Send(new MapRegionListAssetsByMapIdQuery(map));

                            if (mapConfig == null || waypoints == null || !waypoints.Regions.Any())
                            {
                                client.Send(new SystemMessagePacket($"Map information not found for map Id {map}."));
                                _logger.Warning($"Map information not found for map Id {map} on character {client.TamerId} jump booster.");
                                return;
                            }

                            var mapRegionIndex = mapConfig.MapRegionindex;
                            var destination = waypoints.Regions.FirstOrDefault(x => x.Index == mapRegionIndex);

                            _dungeonServer.RemoveClient(dungeonClient);

                            dungeonClient.Tamer.NewLocation(map, destination.X, destination.Y);
                            await _sender.Send(new UpdateCharacterLocationCommand(dungeonClient.Tamer.Location));

                            dungeonClient.Tamer.Partner.NewLocation(map, destination.X, destination.Y);
                            await _sender.Send(new UpdateDigimonLocationCommand(dungeonClient.Tamer.Partner.Location));

                            dungeonClient.Tamer.UpdateState(CharacterStateEnum.Loading);
                            await _sender.Send(new UpdateCharacterStateCommand(dungeonClient.TamerId, CharacterStateEnum.Loading));

                            dungeonClient.SetGameQuit(false);

                            dungeonClient.Send(new MapSwapPacket(_configuration[GamerServerPublic], _configuration[GameServerPort],
                                dungeonClient.Tamer.Location.MapId, dungeonClient.Tamer.Location.X, dungeonClient.Tamer.Location.Y));
                        }

                        foreach (var target in party.Members.Values)
                        {
                            var targetClient = _mapServer.FindClientByTamerId(target.Id);

                            if (targetClient == null) targetClient = _dungeonServer.FindClientByTamerId(target.Id);

                            if (targetClient == null) continue;

                            targetClient.Send(new PartyMemberLeavePacket(leaveTargetKey).Serialize());
                        }

                    }
                
                }
                catch (Exception ex)
                {
                    _logger.Error($"Error: -----------------\n{ex.Message}");
                }
            }
            else
            {
                _logger.Error($"Tamer {client.Tamer.Name} left from the party but he/she was not in the party.");
            }
        }
    }
}