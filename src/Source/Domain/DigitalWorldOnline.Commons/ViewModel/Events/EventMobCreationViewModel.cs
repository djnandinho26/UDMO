﻿using DigitalWorldOnline.Commons.Enums.ClientEnums;
using DigitalWorldOnline.Commons.ViewModel.Mobs;

namespace DigitalWorldOnline.Commons.ViewModel.Events
{
    public class EventMobCreationViewModel
    {
        /// <summary>
        /// Client reference for digimon type.
        /// </summary>
        public int Type { get; set; }

        /// <summary>
        /// Client reference for digimon model.
        /// </summary>
        public int Model { get; set; }

        /// <summary>
        /// Digimon name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Base digimon level.
        /// </summary>
        public byte Level { get; set; }

        /// <summary>
        /// View range (from current position) for aggressive mobs.
        /// </summary>
        public int ViewRange { get; set; }

        /// <summary>
        /// Hunt range (from start position) for giveup on chasing targets.
        /// </summary>
        public int HuntRange { get; set; }

        /// <summary>
        /// Monster class type enumeration. 8 = Raid Boss
        /// </summary>
        public int Class { get; set; }

        /// <summary>
        /// Monster coliseum Round
        /// </summary>
        public byte Round { get; set; }

        /// <summary>
        /// Mob reaction type.
        /// </summary>
        public DigimonReactionTypeEnum ReactionType { get; set; }

        /// <summary>
        /// Mob attribute.
        /// </summary>
        public DigimonAttributeEnum Attribute { get; set; }

        /// <summary>
        /// Mob element.
        /// </summary>
        public DigimonElementEnum Element { get; set; }

        /// <summary>
        /// Mob main family.
        /// </summary>
        public DigimonFamilyEnum Family1 { get; set; }

        /// <summary>
        /// Mob second family.
        /// </summary>
        public DigimonFamilyEnum Family2 { get; set; }

        /// <summary>
        /// Mob third family.
        /// </summary>
        public DigimonFamilyEnum Family3 { get; set; }

        /// <summary>
        /// Respawn interval in seconds.
        /// </summary>
        public int RespawnInterval { get; set; }

        /// <summary>
        /// Monster spawn duration.
        /// </summary>
        public int Duration { get; set; }

        /// <summary>
        /// Total Attack Speed value.
        /// </summary>
        public int ASValue { get; set; }

        /// <summary>
        /// Total Attack Range value.
        /// </summary>
        public int ARValue { get; set; }

        /// <summary>
        /// Total Attack value.
        /// </summary>
        public int ATValue { get; set; }

        /// <summary>
        /// Total Block value.
        /// </summary>
        public int BLValue { get; set; }

        /// <summary>
        /// Total Critical value.
        /// </summary>
        public int CTValue { get; set; } //TODO: separar CR e CD

        /// <summary>
        /// Total Defense value.
        /// </summary>
        public int DEValue { get; set; }

        /// <summary>
        /// Total DigiSoul value.
        /// </summary>
        public int DSValue { get; set; }

        /// <summary>
        /// Total Evasion value.
        /// </summary>
        public int EVValue { get; set; }

        /// <summary>
        /// Total Health value.
        /// </summary>
        public int HPValue { get; set; }

        /// <summary>
        /// Total Hit Rate value.
        /// </summary>
        public int HTValue { get; set; }

        /// <summary>
        /// Total Run Speed value.
        /// </summary>
        public int MSValue { get; set; }

        /// <summary>
        /// Total Walk Speed value.
        /// </summary>
        public int WSValue { get; set; }

        /// <summary>
        /// Initial location.
        /// </summary>
        public MobLocationViewModel Location { get; set; }

        /// <summary>
        /// Drop config.
        /// </summary>
        public MobDropRewardViewModel DropReward { get; set; }

        /// <summary>
        /// Exp config.
        /// </summary>
        public MobExpRewardViewModel ExpReward { get; set; }

        /// <summary>
        /// Flag for empty fields.
        /// </summary>
        public bool Empty => Type == 0 || Model == 0 || string.IsNullOrEmpty(Name);

        public EventMobCreationViewModel()
        {
            ARValue = 150;
            ASValue = 2500;
            HPValue = 1;
            ViewRange = 350;
            HuntRange = 4000;
            Level = 1;
            MSValue = 650;
            WSValue = 350;
            Round = 1;
            RespawnInterval = 5;
            Duration = 0;
            Location = new MobLocationViewModel();
            DropReward = new MobDropRewardViewModel();
            ExpReward = new MobExpRewardViewModel();
        }
    }
}
