using DigitalWorldOnline.Commons.Enums.PacketProcessor;
using DigitalWorldOnline.Commons.Packets.GameServer;
using DigitalWorldOnline.Commons.Interfaces;
using DigitalWorldOnline.Commons.Entities;
using MediatR;
using Serilog;

namespace DigitalWorldOnline.Game.PacketProcessors
{
    public class BurningEventPacketProcessor : IGamePacketProcessor
    {
        public GameServerPacketEnum Type => GameServerPacketEnum.BurningEvent;

        private readonly ISender _sender;
        private readonly ILogger _logger;

        public BurningEventPacketProcessor(ISender sender, ILogger logger)
        {
            _sender = sender;
            _logger = logger;
        }

        public async Task Process(GameClient client, byte[] packetData)
        {
            var packet = new GamePacketReader(packetData);

            _logger.Information($"--- Burning Event Packet 3132 ---\n");

            uint m_nExpRate = packet.ReadUInt();
            uint m_nNextDayExpRate = packet.ReadUInt();
            uint m_nExpTarget = packet.ReadUInt();
            uint m_nSpecialExp = packet.ReadUInt();

            //_logger.Information($"Result: {m_nResult}");
            _logger.Information($"ExpRate: {m_nExpRate} | NextDayExpRate: {m_nNextDayExpRate} | ExpTarget: {m_nExpTarget}\n");

            _logger.Information($"---------------------------------");

            await _sender.Send(new BurningEventPacket(0, m_nExpRate, m_nNextDayExpRate, m_nExpTarget, m_nSpecialExp));

        }
    }
}
