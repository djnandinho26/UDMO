﻿namespace DigitalWorldOnline.Commons.DTOs.Config.Events
{
    public class EventItemDropConfigDTO
    {
        /// <summary>
        /// Sequencial unique identifier.
        /// </summary>
        public long Id { get; set; }

        /// <summary>
        /// Client item id information.
        /// </summary>
        public int ItemId { get; set; }

        /// <summary>
        /// Min. amount of the item.
        /// </summary>
        public int MinAmount { get; set; }

        /// <summary>
        /// Max. amount of the item.
        /// </summary>
        public int MaxAmount { get; set; }

        /// <summary>
        /// Chance of drop.
        /// </summary>
        public double Chance { get; set; }

        /// <summary>
        /// Reward rak number.
        /// </summary>
        public int Rank { get; set; }

        /// <summary>
        /// Reference to the drop reward.
        /// </summary>
        public long DropRewardId { get; set; }

        public EventMobDropRewardConfigDTO DropReward { get; set; }
    }
}