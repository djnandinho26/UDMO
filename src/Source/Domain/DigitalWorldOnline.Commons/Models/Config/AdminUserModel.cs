﻿using DigitalWorldOnline.Commons.Enums;

namespace DigitalWorldOnline.Commons.Models.Config
{
    public class AdminUserModel
    {
        /// <summary>
        /// Unique sequential identifier.
        /// </summary>
        public long Id { get; set; }

        /// <summary>
        /// The user username.
        /// </summary>
        public string Username { get; set; }

        /// <summary>
        /// The user password.
        /// </summary>
        public string Password { get; set; }

        /// <summary>
        /// The user access permission level.
        /// </summary>
        public UserAccessLevelEnum AccessLevel { get; set; }
    }
}