using AutoMapper;
using DigitalWorldOnline.Application;
using DigitalWorldOnline.Application.Separar.Commands.Update;
using DigitalWorldOnline.Application.Separar.Queries;
using DigitalWorldOnline.Commons.Entities;
using DigitalWorldOnline.Commons.Enums;
using DigitalWorldOnline.Commons.Enums.Character;
using DigitalWorldOnline.Commons.Enums.PacketProcessor;
using DigitalWorldOnline.Commons.Interfaces;
using DigitalWorldOnline.Commons.Models.Account;
using DigitalWorldOnline.Commons.Models.Base;
using DigitalWorldOnline.Commons.Models.Character;
using DigitalWorldOnline.Commons.Packets.GameServer;
using DigitalWorldOnline.Commons.Utils;
using DigitalWorldOnline.Game.Managers;
using DigitalWorldOnline.GameHost;
using DigitalWorldOnline.GameHost.EventsServer;
using MediatR;
using Microsoft.IdentityModel.Tokens;
using Serilog;

namespace DigitalWorldOnline.Game.PacketProcessors
{
    public class InitialInformationPacketProcessor : IGamePacketProcessor
    {
        public GameServerPacketEnum Type => GameServerPacketEnum.InitialInformation;

        private readonly PartyManager _partyManager;
        private readonly StatusManager _statusManager;
        private readonly MapServer _mapServer;
        private readonly PvpServer _pvpServer;
        private readonly EventServer _eventServer;
        private readonly DungeonsServer _dungeonsServer;

        private readonly AssetsLoader _assets;
        private readonly ILogger _logger;
        private readonly ISender _sender;
        private readonly IMapper _mapper;

        public InitialInformationPacketProcessor(
            PartyManager partyManager,
            StatusManager statusManager,
            MapServer mapServer,
            PvpServer pvpServer,
            EventServer eventServer,
            DungeonsServer dungeonsServer,
            AssetsLoader assets,
            ILogger logger,
            ISender sender,
            IMapper mapper)
        {
            _partyManager = partyManager;
            _statusManager = statusManager;
            _mapServer = mapServer;
            _pvpServer = pvpServer;
            _eventServer = eventServer;
            _dungeonsServer = dungeonsServer;
            _assets = assets;
            _logger = logger;
            _sender = sender;
            _mapper = mapper;
        }

        public async Task Process(GameClient client, byte[] packetData)
        {
            var packet = new GamePacketReader(packetData);

            //_logger.Information($"Runing InitialInformationPacketProcessor ... **************************");

            packet.Skip(4);
            var accountId = packet.ReadUInt();
            var accessCode = packet.ReadUInt();

            var account = _mapper.Map<AccountModel>(await _sender.Send(new AccountByIdQuery(accountId)));
            client.SetAccountInfo(account);

            try
            {
                CharacterModel? character =
                    _mapper.Map<CharacterModel>(
                        await _sender.Send(new CharacterByIdQuery(account.LastPlayedCharacter)));

                _logger.Information(
                    $"Search character with id {account.LastPlayedCharacter} for account {account.Id}...");

                if (character == null || character.Partner == null)
                {
                    _logger.Information($"Invalid character information for tamer id {account.LastPlayedCharacter}.");
                    return;
                }

                account.ItemList.ForEach(character.AddItemList);

                foreach (var digimon in character.Digimons)
                {
                    digimon.SetTamer(character);

                    digimon.SetBaseInfo(_statusManager.GetDigimonBaseInfo(digimon.CurrentType));

                    digimon.SetBaseStatus(
                        _statusManager.GetDigimonBaseStatus(digimon.CurrentType, digimon.Level, digimon.Size));

                    digimon.SetTitleStatus(_statusManager.GetTitleStatus(character.CurrentTitle));

                    digimon.SetSealStatus(_assets.SealInfo);
                }

                var tamerLevelStatus = _statusManager.GetTamerLevelStatus(character.Model, character.Level);

                character.SetBaseStatus(_statusManager.GetTamerBaseStatus(character.Model));

                character.SetLevelStatus(tamerLevelStatus);

                character.NewViewLocation(character.Location.X, character.Location.Y);
                character.NewLocation(character.Location.MapId, character.Location.X, character.Location.Y);
                character.Partner.NewLocation(character.Location.MapId, character.Location.X, character.Location.Y);

                character.RemovePartnerPassiveBuff();
                character.SetPartnerPassiveBuff();
                character.Partner.SetTamer(character);

                await _sender.Send(new UpdateDigimonBuffListCommand(character.Partner.BuffList));

                foreach (var item in character.ItemList.SelectMany(x => x.Items).Where(x => x.ItemId > 0))
                    item.SetItemInfo(_assets.ItemInfo.FirstOrDefault(x => x.ItemId == item?.ItemId));

                foreach (var buff in character.BuffList.ActiveBuffs)
                    buff.SetBuffInfo(_assets.BuffInfo.FirstOrDefault(x =>
                        x.SkillCode == buff.SkillId || x.DigimonSkillCode == buff.SkillId));

                foreach (var buff in character.Partner.BuffList.ActiveBuffs)
                    buff.SetBuffInfo(_assets.BuffInfo.FirstOrDefault(x =>
                        x.SkillCode == buff.SkillId || x.DigimonSkillCode == buff.SkillId));

                //await _sender.Send(new UpdateCharacterBuffListCommand(client.Tamer.BuffList));

                _logger.Debug($"Getting available channels...");

                bool isDungeonMap = UtilitiesFunctions.DungeonMapIds.Contains(character?.Location.MapId ?? 0);
                if (isDungeonMap)
                {
                    character.SetCurrentChannel(0);
                }
                else
                {
                    var channels =
                        (Dictionary<byte, byte>)await _sender.Send(new ChannelsByMapIdQuery(character.Location.MapId));
                    byte? channel = GetTargetChannel(character.Channel, channels);

                    if (channel == null)
                    {
                        _logger.Debug($"Creating new channel for map {character.Location.MapId}...");
                        channel = CreateNewChannelForMap(channels);
                    }

                    if (character.Channel == byte.MaxValue)
                    {
                        character.SetCurrentChannel(channel.Value);
                    }
                }

                character.UpdateState(CharacterStateEnum.Loading);
                client.SetCharacter(character);
                client.SetSentOnceDataSent(character.InitialPacketSentOnceSent);
                character.SetGenericHandler(character.Partner.GeneralHandler);

                var party = _partyManager.FindParty(client.TamerId);

                if (party != null)
                {
                    party.UpdateMember(party[client.TamerId], character);

                    var firstMemberLocation =
                        party.Members.Values.FirstOrDefault(x => x.Location.MapId == client.Tamer.Location.MapId);
                    
                    if (firstMemberLocation != null)
                    {
                        character.SetCurrentChannel(firstMemberLocation.Channel);
                        client.Tamer.SetCurrentChannel(firstMemberLocation.Channel);
                    }

                    foreach (var target in party.Members.Values.Where(x => x.Id != client.TamerId))
                    {
                        var targetClient = _mapServer.FindClientByTamerId(target.Id);
                        if (targetClient == null) targetClient = _dungeonsServer.FindClientByTamerId(target.Id);

                        if (targetClient == null) continue;

                        KeyValuePair<byte, CharacterModel> partyMember =
                            party.Members.FirstOrDefault(x => x.Value.Id == client.TamerId);
                        targetClient.Send(
                            UtilitiesFunctions.GroupPackets(
                                new PartyMemberWarpGatePacket(partyMember, targetClient.Tamer).Serialize(),
                                new PartyMemberMovimentationPacket(partyMember).Serialize()
                            ));
                    }
                }

                _logger.Debug($"Updating character state...");
                await _sender.Send(new UpdateCharacterStateCommand(character.Id, CharacterStateEnum.Loading));
                var mapConfig = await _sender.Send(new GameMapConfigByMapIdQuery(client.Tamer.Location.MapId));
                if (mapConfig == null)
                {
                    await _mapServer.AddClient(client);
                    _logger.Information(
                        $"Adding character {character.Name}({character.Id}) to map {character.Location.MapId} {character.GeneralHandler} - {character.Partner.GeneralHandler} on Channel {character.Channel}...");
                }
                else
                {
                    if (client.DungeonMap)
                    {
                        await _dungeonsServer.AddClient(client);
                        _logger.Information(
                            $"Adding character {character.Name}({character.Id}) to map {character.Location.MapId} {character.GeneralHandler} - {character.Partner.GeneralHandler}...");
                    }
                    else
                    {
                        switch (mapConfig.Type)
                        {
                            case MapTypeEnum.Dungeon:
                                await _dungeonsServer.AddClient(client);
                                _logger.Information(
                                    $"Adding character {character.Name}({character.Id}) to map {character.Location.MapId} {character.GeneralHandler} - {character.Partner.GeneralHandler}...");
                                break;
                            case MapTypeEnum.Pvp:
                                await _pvpServer.AddClient(client);
                                _logger.Information(
                                    $"Adding character {character.Name}({character.Id}) to map {character.Location.MapId} {character.GeneralHandler} - {character.Partner.GeneralHandler}...");
                                break;
                            case MapTypeEnum.Event:
                                await _dungeonsServer.AddClient(client);
                                _logger.Information(
                                    $"Adding character {character.Name}({character.Id}) to map {character.Location.MapId} {character.GeneralHandler} - {character.Partner.GeneralHandler}...");
                                break;
                            case MapTypeEnum.Default:
                            default:
                                await _mapServer.AddClient(client);
                                _logger.Information(
                                    $"Adding character {character.Name}({character.Id}) to map {character.Location.MapId} {character.GeneralHandler} - {character.Partner.GeneralHandler} on Channel {character.Channel}...");
                                break;
                        }
                    }
                }

                while (client.Loading) await Task.Delay(1000);

                if (!client.DungeonMap)
                {
                    var region = _assets.Maps.FirstOrDefault(x => x.MapId == character.Location.MapId);

                    if (region != null)
                    {
                        if (character.MapRegions[region.RegionIndex].Unlocked != 0x80)
                        {
                            var characterRegion = character.MapRegions[region.RegionIndex];
                            characterRegion.Unlock();

                            await _sender.Send(new UpdateCharacterMapRegionCommand(characterRegion));
                        }
                    }
                }

                await ReceiveArenaPoints(client);

                client.Send(new InitialInfoPacket(character, party));

                await _sender.Send(new ChangeTamerIdTPCommand(client.Tamer.Id, (int)0));

                _logger.Debug($"Updating character channel...");
                await _sender.Send(new UpdateCharacterChannelCommand(character.Id, character.Channel));
            }
            catch (Exception ex)
            {
                _logger.Error(
                    $"[{account.LastPlayedCharacter}] An error occurred: {ex.Message}, Line: {ex.Source.ToString()}, Stacktrace: {ex.StackTrace.ToString()}",
                    ex);
                client.Disconnect();
            }
        }

        private async Task ReceiveArenaPoints(GameClient client)
        {
            if (client.Tamer.Points.Amount > 0)
            {
                var newItem = new ItemModel();
                newItem.SetItemInfo(_assets.ItemInfo.FirstOrDefault(x => x.ItemId == client.Tamer.Points.ItemId));

                newItem.ItemId = client.Tamer.Points.ItemId;
                newItem.Amount = client.Tamer.Points.Amount;

                if (newItem.IsTemporary)
                    newItem.SetRemainingTime((uint)newItem.ItemInfo.UsageTimeMinutes);

                var itemClone = (ItemModel)newItem.Clone();

                if (client.Tamer.Inventory.AddItem(newItem))
                {
                    await _sender.Send(new UpdateItemsCommand(client.Tamer.Inventory));
                }
                else
                {
                    newItem.EndDate = DateTime.Now.AddDays(7);

                    client.Tamer.GiftWarehouse.AddItem(newItem);
                    await _sender.Send(new UpdateItemsCommand(client.Tamer.GiftWarehouse));
                }

                client.Tamer.Points.SetAmount(0);
                client.Tamer.Points.SetCurrentStage(0);

                await _sender.Send(new UpdateCharacterArenaPointsCommand(client.Tamer.Points));
            }
            else if (client.Tamer.Points.CurrentStage > 0)
            {
                client.Tamer.Points.SetCurrentStage(0);
                await _sender.Send(new UpdateCharacterArenaPointsCommand(client.Tamer.Points));
            }
        }

        private byte? GetTargetChannel(byte currentChannel, Dictionary<byte, byte> channels)
        {
            if (currentChannel == byte.MaxValue && !channels.IsNullOrEmpty())
            {
                return SelectRandomChannel(channels.Keys);
            }

            return currentChannel == byte.MaxValue ? null : (byte?)currentChannel;
        }

        private byte SelectRandomChannel(IEnumerable<byte> channelKeys)
        {
            var random = new Random();
            var keys = channelKeys.ToList();
            return keys[random.Next(keys.Count)];
        }

        private byte CreateNewChannelForMap(Dictionary<byte, byte> channels)
        {
            channels.Add(channels.Keys.GetNewChannel(), 1);
            return channels
                .OrderByDescending(x => x.Value)
                .First(x => x.Value < byte.MaxValue)
                .Key;
        }
    }
}