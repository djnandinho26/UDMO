using DigitalWorldOnline.Application.Separar.Commands.Update;
using DigitalWorldOnline.Commons.Entities;
using DigitalWorldOnline.Commons.Enums;
using DigitalWorldOnline.Commons.Enums.ClientEnums;
using DigitalWorldOnline.Commons.Models.Base;
using DigitalWorldOnline.Commons.Models.Character;
using DigitalWorldOnline.Commons.Models.Config;
using DigitalWorldOnline.Commons.Models.Digimon;
using DigitalWorldOnline.Commons.Models.Map;
using DigitalWorldOnline.Commons.Packets.GameServer;
using DigitalWorldOnline.Commons.Packets.GameServer.Combat;
using DigitalWorldOnline.Commons.Packets.Items;
using DigitalWorldOnline.Commons.Packets.MapServer;
using DigitalWorldOnline.Commons.Utils;
using DigitalWorldOnline.Game.Managers;

using System.Text;

namespace DigitalWorldOnline.GameHost.EventsServer
{
    public sealed partial class EventServer
    {
        public Task TamerOperation(GameMap map)
        {
            if (!map.ConnectedTamers.Any())
            {
                map.SetNoTamers();
                return Task.CompletedTask;
            }

            foreach (var tamer in map.ConnectedTamers)
            {
                var client = map.Clients.FirstOrDefault(x => x.TamerId == tamer.Id);

                if (client == null || !client.IsConnected || client.Partner == null)
                    continue;

                GetInViewMobs(map, tamer);

                ShowOrHideTamer(map, tamer);

                if (tamer.TargetMobs.Count > 0)
                {
                    _logger.Debug($"Target found !!");
                    PartnerAutoAttackMob(tamer);
                }
                    
                CheckTimeReward(client);

                tamer.AutoRegen();
                tamer.ActiveEvolutionReduction();

                if (tamer.BreakEvolution)
                {
                    tamer.ActiveEvolution.SetDs(0);
                    tamer.ActiveEvolution.SetXg(0);

                    if (tamer.Riding)
                    {
                        tamer.StopRideMode();

                        BroadcastForTamerViewsAndSelf(tamer.Id, new UpdateMovementSpeedPacket(tamer).Serialize());
                        BroadcastForTamerViewsAndSelf(tamer.Id, new RideModeStopPacket(tamer.GeneralHandler, tamer.Partner.GeneralHandler).Serialize());
                    }

                    map.BroadcastForTamerViewsAndSelf(tamer.Id,
                        new DigimonEvolutionSucessPacket(tamer.GeneralHandler,
                        tamer.Partner.GeneralHandler,
                        tamer.Partner.BaseType,
                        DigimonEvolutionEffectEnum.Back).Serialize());

                    var currentHp = client.Partner.CurrentHp;
                    var currentMaxHp = client.Partner.HP;
                    var currentDs = client.Partner.CurrentDs;
                    var currentMaxDs = client.Partner.DS;

                    tamer.Partner.UpdateCurrentType(tamer.Partner.BaseType);

                    tamer.Partner.SetBaseInfo(_statusManager.GetDigimonBaseInfo(tamer.Partner.CurrentType));

                    tamer.Partner.SetBaseStatus(
                        _statusManager.GetDigimonBaseStatus(
                            tamer.Partner.CurrentType,
                            tamer.Partner.Level,
                            tamer.Partner.Size
                        )
                    );

                    client.Partner.AdjustHpAndDs(currentHp, currentMaxHp, currentDs, currentMaxDs);

                    client.Send(new UpdateStatusPacket(tamer));
                    _sender.Send(new UpdateCharacterBasicInfoCommand(client.Tamer));
                }

                if (tamer.CheckBuffsTime)
                {
                    tamer.UpdateBuffsCheckTime();

                    if (tamer.BuffList.HasActiveBuffs)
                    {
                        var buffsToRemove = tamer.BuffList.Buffs
                            .Where(x => x.Expired)
                            .ToList();

                        buffsToRemove.ForEach(buffToRemove =>
                        { map.BroadcastForTamerViewsAndSelf(tamer.Id, new RemoveBuffPacket(tamer.GeneralHandler, buffToRemove.BuffId).Serialize()); });

                        if (buffsToRemove.Any())
                        {
                            client?.Send(new UpdateStatusPacket(tamer));
                            map.BroadcastForTargetTamers(map.TamersView[tamer.Id], new UpdateCurrentHPRatePacket(tamer.GeneralHandler, tamer.HpRate).Serialize());
                        }
                    }

                    if (tamer.Partner.BuffList.HasActiveBuffs)
                    {
                        var buffsToRemove = tamer.Partner.BuffList.Buffs
                            .Where(x => x.Expired)
                            .ToList();

                        buffsToRemove.ForEach(buffToRemove =>
                        { map.BroadcastForTamerViewsAndSelf(tamer.Id, new RemoveBuffPacket(tamer.Partner.GeneralHandler, buffToRemove.BuffId).Serialize()); });

                        if (buffsToRemove.Any())
                        {

                            client?.Send(new UpdateStatusPacket(tamer));
                            map.BroadcastForTargetTamers(map.TamersView[tamer.Id], new UpdateCurrentHPRatePacket(tamer.Partner.GeneralHandler, tamer.Partner.HpRate).Serialize());
                        }
                    }
                }

                if (tamer.SyncResourcesTime)
                {
                    tamer.UpdateSyncResourcesTime();

                    client?.Send(new UpdateCurrentResourcesPacket(tamer.GeneralHandler, (short)tamer.CurrentHp, (short)tamer.CurrentDs, (short)CharacterModel.Fatigue));
                    client?.Send(new UpdateCurrentResourcesPacket(tamer.Partner.GeneralHandler, (short)tamer.Partner.CurrentHp, (short)tamer.Partner.CurrentDs, 0));
                    client?.Send(new TamerXaiResourcesPacket(client.Tamer.XGauge, client.Tamer.XCrystals));

                    map.BroadcastForTargetTamers(map.TamersView[tamer.Id], new UpdateCurrentHPRatePacket(tamer.GeneralHandler, tamer.HpRate).Serialize());
                    map.BroadcastForTargetTamers(map.TamersView[tamer.Id], new UpdateCurrentHPRatePacket(tamer.Partner.GeneralHandler, tamer.Partner.HpRate).Serialize());
                    map.BroadcastForTamerViewsAndSelf(tamer.Id, new SyncConditionPacket(tamer.GeneralHandler, tamer.CurrentCondition, tamer.ShopName).Serialize());
                }

            }

            return Task.CompletedTask;
        }

        private void GetInViewMobs(GameMap map, CharacterModel tamer)
        {
            map.Mobs.ForEach(mob =>
            {
                var distanceDifference = UtilitiesFunctions.CalculateDistance(
                    tamer.Location.X,
                    mob.CurrentLocation.X,
                    tamer.Location.Y,
                    mob.CurrentLocation.Y);

                if (distanceDifference <= _startToSee && !tamer.MobsInView.Contains(mob.Id))
                    tamer.MobsInView.Add(mob.Id);

                if (distanceDifference >= _stopSeeing && tamer.MobsInView.Contains(mob.Id))
                    tamer.MobsInView.Remove(mob.Id);
            });
        }

        // ------------------------------------------------------------------------------------

        public void SwapDigimonHandlers(int mapId, DigimonModel oldPartner, DigimonModel newPartner)
        {
            Maps.FirstOrDefault(x => x.MapId == mapId)?.SwapDigimonHandlers(oldPartner, newPartner);
        }

        public void SwapDigimonHandlers(int mapId, int channel, DigimonModel oldPartner, DigimonModel newPartner)
        {
            Maps.FirstOrDefault(x => x.MapId == mapId && x.Channel == channel)?.SwapDigimonHandlers(oldPartner, newPartner);
        }

        // ------------------------------------------------------------------------------------

        private void ShowOrHideTamer(GameMap map, CharacterModel tamer)
        {
            foreach (var connectedTamer in map.ConnectedTamers.Where(x => x.Id != tamer.Id))
            {
                var distanceDifference = UtilitiesFunctions.CalculateDistance(
                    tamer.Location.X,
                    connectedTamer.Location.X,
                    tamer.Location.Y,
                    connectedTamer.Location.Y);

                if (distanceDifference <= _startToSee)
                    ShowTamer(map, tamer, connectedTamer.Id);
                else if (distanceDifference >= _stopSeeing)
                    HideTamer(map, tamer, connectedTamer.Id);
            }
        }

        private void ShowTamer(GameMap map, CharacterModel tamerToShow, long tamerToSeeId)
        {
            if (!map.ViewingTamer(tamerToShow.Id, tamerToSeeId))
            {
                foreach (var item in tamerToShow.Equipment.EquippedItems.Where(x => x.ItemInfo == null))
                    item?.SetItemInfo(_assets.ItemInfo.FirstOrDefault(x => x.ItemId == item?.ItemId));

                map.ShowTamer(tamerToShow.Id, tamerToSeeId);

                var targetClient = map.Clients.FirstOrDefault(x => x.TamerId == tamerToSeeId);
                if (targetClient != null)
                {
                    targetClient.Send(new LoadTamerPacket(tamerToShow));
#if DEBUG
                    var serialized = SerializeShowTamer(tamerToShow);
                    File.WriteAllText($"Shows\\Show{tamerToShow.Id}To{tamerToSeeId}_{DateTime.Now:dd_MM_yy_HH_mm_ss}.temp", serialized);
#endif
                }
            }
        }

        private void HideTamer(GameMap map, CharacterModel tamerToHide, long tamerToBlindId)
        {
            if (map.ViewingTamer(tamerToHide.Id, tamerToBlindId))
            {
                map.HideTamer(tamerToHide.Id, tamerToBlindId);

                var targetClient = map.Clients.FirstOrDefault(x => x.TamerId == tamerToBlindId);

                if (targetClient != null)
                {
                    targetClient.Send(new UnloadTamerPacket(tamerToHide));

#if DEBUG
                    var serialized = SerializeHideTamer(tamerToHide);
                    File.WriteAllText($"Hides\\Hide{tamerToHide.Id}To{tamerToBlindId}_{DateTime.Now:dd_MM_yy_HH_mm_ss}.temp", serialized);
#endif
                }
            }
        }

        // ------------------------------------------------------------------------------------

        private static string SerializeHideTamer(CharacterModel tamer)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Tamer{tamer.Id}{tamer.Name}");
            sb.AppendLine($"TamerHandler {tamer.GeneralHandler.ToString()}");
            sb.AppendLine($"TamerLocation {tamer.Location.X.ToString()}");
            sb.AppendLine($"TamerLocation {tamer.Location.Y.ToString()}");

            sb.AppendLine($"Partner{tamer.Partner.Id}{tamer.Partner.Name}");
            sb.AppendLine($"PartnerHandler {tamer.Partner.GeneralHandler.ToString()}");
            sb.AppendLine($"PartnerLocation {tamer.Partner.Location.X.ToString()}");
            sb.AppendLine($"PartnerLocation {tamer.Partner.Location.Y.ToString()}");

            return sb.ToString();
        }

        private static string SerializeShowTamer(CharacterModel tamer)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Partner{tamer.Partner.Id}");
            sb.AppendLine($"PartnerName {tamer.Partner.Name}");
            sb.AppendLine($"PartnerLocation {tamer.Partner.Location.X.ToString()}");
            sb.AppendLine($"PartnerLocation {tamer.Partner.Location.Y.ToString()}");
            sb.AppendLine($"PartnerHandler {tamer.Partner.GeneralHandler.ToString()}");
            sb.AppendLine($"PartnerCurrentType {tamer.Partner.CurrentType.ToString()}");
            sb.AppendLine($"PartnerSize {tamer.Partner.Size.ToString()}");
            sb.AppendLine($"PartnerLevel {tamer.Partner.Level.ToString()}");
            sb.AppendLine($"PartnerModel {tamer.Partner.Model.ToString()}");
            sb.AppendLine($"PartnerMS {tamer.Partner.MS.ToString()}");
            sb.AppendLine($"PartnerAS {tamer.Partner.AS.ToString()}");
            sb.AppendLine($"PartnerHPRate {tamer.Partner.HpRate.ToString()}");
            sb.AppendLine($"PartnerCloneTotalLv {tamer.Partner.Digiclone.CloneLevel.ToString()}");
            sb.AppendLine($"PartnerCloneAtLv {tamer.Partner.Digiclone.ATLevel.ToString()}");
            sb.AppendLine($"PartnerCloneBlLv {tamer.Partner.Digiclone.BLLevel.ToString()}");
            sb.AppendLine($"PartnerCloneCtLv {tamer.Partner.Digiclone.CTLevel.ToString()}");
            sb.AppendLine($"PartnerCloneEvLv {tamer.Partner.Digiclone.EVLevel.ToString()}");
            sb.AppendLine($"PartnerCloneHpLv {tamer.Partner.Digiclone.HPLevel.ToString()}");

            sb.AppendLine($"Tamer{tamer.Id}");
            sb.AppendLine($"TamerName {tamer.Name.ToString()}");
            sb.AppendLine($"TamerLocation {tamer.Location.X.ToString()}");
            sb.AppendLine($"TamerLocation {tamer.Location.Y.ToString()}");
            sb.AppendLine($"TamerHandler {tamer.GeneralHandler.ToString()}");
            sb.AppendLine($"TamerModel {tamer.Model.ToString()}");
            sb.AppendLine($"TamerLevel {tamer.Level.ToString()}");
            sb.AppendLine($"TamerMS {tamer.MS.ToString()}");
            sb.AppendLine($"TamerHpRate {tamer.HpRate.ToString()}");
            sb.AppendLine($"TamerEquipment {tamer.Equipment.ToString()}");
            sb.AppendLine($"TamerDigivice {tamer.Digivice.ToString()}");
            sb.AppendLine($"TamerCurrentCondition {tamer.CurrentCondition.ToString()}");
            sb.AppendLine($"TamerSize {tamer.Size.ToString()}");
            sb.AppendLine($"TamerCurrentTitle {tamer.CurrentTitle.ToString()}");
            sb.AppendLine($"TamerSealLeaderId {tamer.SealList.SealLeaderId.ToString()}");

            return sb.ToString();
        }

        // ------------------------------------------------------------------------------------

        public void PartnerAutoAttackMob(CharacterModel tamer)
        {
            if (!tamer.Partner.AutoAttack)
                return;

            if (!tamer.Partner.IsAttacking && tamer.TargetMob != null && tamer.TargetMob.Alive & tamer.Partner.Alive)
            {
                tamer.Partner.SetEndAttacking(tamer.Partner.AS);
                tamer.SetHidden(false);

                if (!tamer.InBattle && tamer.TargetMob != null)
                {
                    _logger.Debug($"Character {tamer.Id} engaged {tamer.TargetMob.Id} - {tamer.TargetMob.Name}.");
                    BroadcastForTamerViewsAndSelf(tamer.Id,
                        new SetCombatOnPacket(tamer.Partner.GeneralHandler).Serialize());
                    tamer.StartBattle(tamer.TargetMob);
                    tamer.Partner.StartAutoAttack();
                }

                if (!tamer.TargetMob.InBattle && tamer.TargetMob != null)
                {
                    _logger.Debug($"Mob {tamer.TargetMob.Name} engaged battle with {tamer.Partner.Name}.");
                    BroadcastForTamerViewsAndSelf(tamer.Id,
                        new SetCombatOnPacket(tamer.TargetMob.GeneralHandler).Serialize());
                    tamer.TargetMob.StartBattle(tamer);
                    //tamer.Partner.StartAutoAttack();
                }

                var missed = false;

                if (!tamer.GodMode)
                {
                    missed = tamer.CanMissHit();
                }

                if (missed)
                {
                    _logger.Verbose(
                        $"Partner {tamer.Partner.Id} missed hit on {tamer.TargetMob.Id} - {tamer.TargetMob.Name}.");
                    BroadcastForTamerViewsAndSelf(tamer.Id,
                        new MissHitPacket(tamer.Partner.GeneralHandler, tamer.TargetMob.GeneralHandler).Serialize());
                }
                else
                {
                    #region Hit Damage

                    var critBonusMultiplier = 0.00;
                    var blocked = false;
                    var finalDmg = tamer.GodMode
                        ? tamer.TargetMob.CurrentHP
                        : CalculateDamageMob(tamer, out critBonusMultiplier, out blocked);

                    #endregion

                    if (finalDmg <= 0) finalDmg = 1;
                    if (finalDmg > tamer.TargetMob.CurrentHP) finalDmg = tamer.TargetMob.CurrentHP;

                    var newHp = tamer.TargetMob.ReceiveDamage(finalDmg, tamer.Id);

                    var hitType = blocked ? 2 : critBonusMultiplier > 0 ? 1 : 0;

                    if (newHp > 0)
                    {
                        _logger.Verbose(
                            $"Partner {tamer.Partner.Id} inflicted {finalDmg} to mob {tamer.TargetMob?.Id} - {tamer.TargetMob?.Name}({tamer.TargetMob?.Type}).");

                        BroadcastForTamerViewsAndSelf(
                            tamer.Id,
                            new HitPacket(
                                tamer.Partner.GeneralHandler,
                                tamer.TargetMob.GeneralHandler,
                                finalDmg,
                                tamer.TargetMob.HPValue,
                                newHp,
                                hitType).Serialize());
                    }
                    else
                    {
                        _logger.Verbose(
                            $"Partner {tamer.Partner.Id} killed mob {tamer.TargetMob?.Id} - {tamer.TargetMob?.Name}({tamer.TargetMob?.Type}) with {finalDmg} damage.");

                        BroadcastForTamerViewsAndSelf(
                            tamer.Id,
                            new KillOnHitPacket(
                                tamer.Partner.GeneralHandler,
                                tamer.TargetMob.GeneralHandler,
                                finalDmg,
                                hitType).Serialize());

                        tamer.TargetMob?.Die();

                        if (!MobsAttacking(tamer.Location.MapId, tamer.Id))
                        {
                            tamer.StopBattle();

                            BroadcastForTamerViewsAndSelf(
                                tamer.Id,
                                new SetCombatOffPacket(tamer.Partner.GeneralHandler).Serialize());
                        }
                    }
                }

                tamer.Partner.UpdateLastHitTime();
            }

            bool StopAttackMob = tamer.TargetMob == null || tamer.TargetMob.Dead;

            if (StopAttackMob) tamer.Partner?.StopAutoAttack();
        }

        // ------------------------------------------------------------------------------------

        private static int CalculateDamageMob(CharacterModel tamer, out double critBonusMultiplier, out bool blocked)
        {
            int baseDamage = tamer.Partner.AT - tamer.TargetMob.DEValue;

            if (baseDamage < tamer.Partner.AT * 0.5) // If Damage is less than 50% of AT
            {
                baseDamage = (int)(tamer.Partner.AT * 0.9); // give 90% of AT as Damage
            }

            // -------------------------------------------------------------------------------

            critBonusMultiplier = 0.00;
            double critChance = tamer.Partner.CC / 100;

            if (critChance >= UtilitiesFunctions.RandomDouble())
            {
                blocked = false;

                var critDamageMultiplier = tamer.Partner.CD / 100.0;
                critBonusMultiplier = baseDamage * (critDamageMultiplier / 100);
            }

            if (tamer.TargetMob != null)
            {
                blocked = tamer.TargetMob.BLValue >= UtilitiesFunctions.RandomDouble();
            }
            else
            {
                blocked = false;
                return 0;
            }

            // -------------------------------------------------------------------------------

            // Level Diference
            var levelBonusMultiplier = 0;
            //var levelDifference = client.Tamer.Partner.Level - targetMob.Level;
            //var levelBonusMultiplier = levelDifference > 0 ? levelDifference * 0.02 : levelDifference * 0.01;

            // Attribute
            var attributeMultiplier = 0.00;
            if (tamer.Partner.BaseInfo.Attribute.HasAttributeAdvantage(tamer.TargetMob.Attribute))
            {
                var attExp = tamer.Partner.GetAttributeExperience();
                var attValue = tamer.Partner.ATT / 100.0;
                var attValuePercent = attValue / 100.0;
                var bonusMax = 1;
                var expMax = 10000;

                attributeMultiplier = ((bonusMax + attValuePercent) * attExp) / expMax;
            }
            else if (tamer.TargetMob.Attribute.HasAttributeAdvantage(tamer.Partner.BaseInfo.Attribute))
            {
                attributeMultiplier = -0.25;
            }

            // Element
            var elementMultiplier = 0.00;
            if (tamer.Partner.BaseInfo.Element.HasElementAdvantage(tamer.TargetMob.Element))
            {
                var vlrAtual = tamer.Partner.GetElementExperience();
                var bonusMax = 1;
                var expMax = 10000;

                elementMultiplier = (bonusMax * vlrAtual) / expMax;
            }
            else if (tamer.TargetMob.Element.HasElementAdvantage(tamer.Partner.BaseInfo.Element))
            {
                elementMultiplier = -0.25;
            }

            // -------------------------------------------------------------------------------

            if (blocked)
                baseDamage /= 2;

            return (int)Math.Max(1, Math.Floor(baseDamage + critBonusMultiplier +
                                               (baseDamage * levelBonusMultiplier) +
                                               (baseDamage * attributeMultiplier) + (baseDamage * elementMultiplier)));
        }

        // ------------------------------------------------------------------------------------

        private ReceiveExpResult ReceiveTamerExp(CharacterModel tamer, long tamerExpToReceive)
        {
            var tamerResult = _expManager.ReceiveTamerExperience(tamerExpToReceive, tamer);

            if (tamerResult.LevelGain > 0)
            {
                BroadcastForTamerViewsAndSelf(tamer.Id,
                    new LevelUpPacket(tamer.GeneralHandler, tamer.Level).Serialize());

                tamer.SetLevelStatus(
                    _statusManager.GetTamerLevelStatus(
                        tamer.Model,
                        tamer.Level
                    )
                );

                tamer.FullHeal();
            }

            return tamerResult;
        }

        private ReceiveExpResult ReceivePartnerExp(DigimonModel partner, MobConfigModel targetMob, long partnerExpToReceive)
        {
            var partnerResult = _expManager.ReceiveDigimonExperience(partnerExpToReceive, partner);

            _expManager.ReceiveAttributeExperience(partner, targetMob.Attribute, targetMob.Element, targetMob.ExpReward);

            if (partnerResult.LevelGain > 0)
            {
                partner.SetBaseStatus(
                    _statusManager.GetDigimonBaseStatus(
                        partner.CurrentType,
                        partner.Level,
                        partner.Size
                    )
                );

                BroadcastForTamerViewsAndSelf(partner.Character.Id,
                    new LevelUpPacket(partner.GeneralHandler, partner.Level).Serialize());

                partner.FullHeal();
            }

            return partnerResult;
        }

        // ------------------------------------------------------------------------------------

        private async void CheckTimeReward(GameClient client)
        {
            if (client.Tamer.TimeReward.ReedemRewards)
            {
                _logger.Debug($"Reward Index: {client.Tamer.TimeReward.RewardIndex}");
                client.Tamer.TimeReward.SetStartTime();
                await _sender.Send(new UpdateTamerAttendanceTimeRewardCommand(client.Tamer.TimeReward));
            }

            if (client.Tamer.TimeReward.RewardIndex <= TimeRewardIndexEnum.Fourth)
            {
                if (client.Tamer.TimeReward.CurrentTime == 0)
                {
                    client.Tamer.TimeReward.CurrentTime = client.Tamer.TimeReward.AtualTime;
                }

                if (DateTime.Now >= client.Tamer.TimeReward.LastTimeRewardUpdate)
                {
                    //_logger.Information($"CurrentTime: {client.Tamer.TimeReward.CurrentTime} | AtualTime: {client.Tamer.TimeReward.AtualTime}");

                    client.Tamer.TimeReward.CurrentTime++;
                    client.Tamer.TimeReward.UpdateCounter++;
                    client.Tamer.TimeReward.SetAtualTime();

                    if (client.Tamer.TimeReward.TimeCompleted())
                    {
                        ReedemTimeReward(client);
                        client.Tamer.TimeReward.RewardIndex++;
                        client.Tamer.TimeReward.CurrentTime = 0;
                        client.Tamer.TimeReward.SetAtualTime();

                        await _sender.Send(new UpdateTamerAttendanceTimeRewardCommand(client.Tamer.TimeReward));
                    }
                    else if (client.Tamer.TimeReward.UpdateCounter >= 5)
                    {
                        await _sender.Send(new UpdateTamerAttendanceTimeRewardCommand(client.Tamer.TimeReward));
                        client.Tamer.TimeReward.UpdateCounter = 0;
                    }

                    client.Send(new TimeRewardPacket(client.Tamer.TimeReward));
                }

                client.Tamer.TimeReward.SetLastTimeRewardDate();
            }
            else
            {
                client.Tamer.TimeReward.RewardIndex = TimeRewardIndexEnum.Ended;
                await _sender.Send(new UpdateTamerAttendanceTimeRewardCommand(client.Tamer.TimeReward));
            }
        }

        private void ReedemTimeReward(GameClient client)
        {
            var reward = new ItemModel();

            switch (client.Tamer.TimeReward.RewardIndex)
            {
                case TimeRewardIndexEnum.First:
                    {
                        var GetPrizes = _assets.TimeRewardAssets
                            .Where(drop => drop.CurrentReward == (int)TimeRewardIndexEnum.First).ToList();

                        GetPrizes.ForEach(drop =>
                        {
                            reward.SetItemInfo(_assets.ItemInfo.FirstOrDefault(x => x.ItemId == drop.ItemId));
                            reward.ItemId = drop.ItemId;
                            reward.Amount = drop.ItemCount;

                            if (reward.IsTemporary)
                                reward.SetRemainingTime((uint)reward.ItemInfo.UsageTimeMinutes);

                            if (client.Tamer.Inventory.AddItem(reward))
                            {
                                client.Send(new ReceiveItemPacket(reward, InventoryTypeEnum.Inventory));
                                _sender.Send(new UpdateItemsCommand(client.Tamer.Inventory));
                            }
                        });
                    }
                    break;

                case TimeRewardIndexEnum.Second:
                    {
                        var GetPrizes = _assets.TimeRewardAssets
                            .Where(drop => drop.CurrentReward == (int)TimeRewardIndexEnum.Second).ToList();

                        GetPrizes.ForEach(drop =>
                        {
                            reward.SetItemInfo(_assets.ItemInfo.FirstOrDefault(x => x.ItemId == drop.ItemId));
                            reward.ItemId = drop.ItemId;
                            reward.Amount = drop.ItemCount;

                            if (reward.IsTemporary)
                                reward.SetRemainingTime((uint)reward.ItemInfo.UsageTimeMinutes);

                            if (client.Tamer.Inventory.AddItem(reward))
                            {
                                client.Send(new ReceiveItemPacket(reward, InventoryTypeEnum.Inventory));
                                _sender.Send(new UpdateItemsCommand(client.Tamer.Inventory));
                            }
                        });
                    }
                    break;

                case TimeRewardIndexEnum.Third:
                    {
                        var GetPrizes = _assets.TimeRewardAssets
                            .Where(drop => drop.CurrentReward == (int)TimeRewardIndexEnum.Third).ToList();

                        GetPrizes.ForEach(drop =>
                        {
                            reward.SetItemInfo(_assets.ItemInfo.FirstOrDefault(x => x.ItemId == drop.ItemId));
                            reward.ItemId = drop.ItemId;
                            reward.Amount = drop.ItemCount;

                            if (reward.IsTemporary)
                                reward.SetRemainingTime((uint)reward.ItemInfo.UsageTimeMinutes);

                            if (client.Tamer.Inventory.AddItem(reward))
                            {
                                client.Send(new ReceiveItemPacket(reward, InventoryTypeEnum.Inventory));
                                _sender.Send(new UpdateItemsCommand(client.Tamer.Inventory));
                            }
                        });
                    }
                    break;

                case TimeRewardIndexEnum.Fourth:
                    {
                        var GetPrizes = _assets.TimeRewardAssets
                            .Where(drop => drop.CurrentReward == (int)TimeRewardIndexEnum.Fourth).ToList();

                        GetPrizes.ForEach(drop =>
                        {
                            reward.SetItemInfo(_assets.ItemInfo.FirstOrDefault(x => x.ItemId == drop.ItemId));
                            reward.ItemId = drop.ItemId;
                            reward.Amount = drop.ItemCount;

                            if (reward.IsTemporary)
                                reward.SetRemainingTime((uint)reward.ItemInfo.UsageTimeMinutes);

                            if (client.Tamer.Inventory.AddItem(reward))
                            {
                                client.Send(new ReceiveItemPacket(reward, InventoryTypeEnum.Inventory));
                                _sender.Send(new UpdateItemsCommand(client.Tamer.Inventory));
                            }
                        });
                    }
                    break;

                default:
                    break;
            }
        }

    }
}