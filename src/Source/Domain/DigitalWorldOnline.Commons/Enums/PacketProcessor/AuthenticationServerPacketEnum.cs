namespace DigitalWorldOnline.Commons.Enums.PacketProcessor
{
    public enum AuthenticationServerPacketEnum
    {
        /// <summary>
        /// Unknown packet.
        /// </summary>
        Unknown = 65437, // -99 as ushort

        /// <summary>
        /// To avoid connection break/interrupt, the client sends this often.
        /// </summary>
        KeepConnection = 65533, // -3 as ushort

        /// <summary>
        /// Request connection with the server.
        /// </summary>
        Connection = 65535, // -1 as ushort

        /// <summary>
        /// Account login request.
        /// </summary>
        LoginRequest = 3301,

        /// <summary>
        /// Changes the current secondary password.
        /// </summary>
        SecondaryPasswordChange = 9806,

        /// <summary>
        /// Checks the secondary password for the account.
        /// </summary>
        SecondaryPasswordCheck = 9804,

        /// <summary>
        /// Creates a new secondary password for the account.
        /// </summary>
        SecondaryPasswordRegister = 9801,

        /// <summary>
        /// Loads server list.
        /// </summary>
        LoadServerList = 1701,

        /// <summary>
        /// Connects to the Character server.
        /// </summary>
        ConnectCharacterServer = 1702,
        
        /// <summary>
        /// Requests resources hash.
        /// </summary>
        ResourcesHash = 10003
    }
}
