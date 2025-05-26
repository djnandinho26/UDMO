using DigitalWorldOnline.Application;
using DigitalWorldOnline.Commons.Entities;
using DigitalWorldOnline.Commons.Enums.PacketProcessor;
using DigitalWorldOnline.Commons.Interfaces;
using Serilog;

namespace DigitalWorldOnline.Game
{
    public sealed partial class GamePacketProcessor : IProcessor, IDisposable
    {
        private readonly IEnumerable<IGamePacketProcessor> _packetProcessors;
        private readonly AssetsLoader _assets;
        private readonly ConfigsLoader _configs;
        private readonly ILogger _logger;

        public GamePacketProcessor(
            IEnumerable<IGamePacketProcessor> packetProcessors,
            AssetsLoader assets,
            ConfigsLoader configs,
            ILogger logger)
        {
            _packetProcessors = packetProcessors;
            _assets = assets;
            _configs = configs;
            _logger = logger;
        }

        /// <summary>
        /// Processar o pacote TCP chegado, enviado do cliente do jogo
        /// </summary>
        /// <param name = "client"> o cliente do jogo, que enviou o pacote </am Param>
        /// <param name = "dados"> o pacote Bytes Array </amon>
        public async Task ProcessPacketAsync(GameClient client, byte[] data)
        {
            while (_assets.Loading || _configs.Loading) await Task.Delay(1000);

            var packet = new GamePacketReader(data);

            switch (packet.Enum)
            {
                case GameServerPacketEnum.Unknown:
                    _logger.Warning($"Pacote desconhecido. Type: {packet.Type}. Length: {packet.Length}.");
                    break;

                default:
                    {
                        var processor = _packetProcessors.FirstOrDefault(x => x.Type == packet.Enum);

                        if (processor != null)
                        {
                            await processor.Process(client, data);
                        }
                        else
                        {
                            _logger.Error($"No processor for packet {packet.Type} para o jogador {client.Tamer.Name}.");
                        }
                    }
                    break;
            }
        }

        /// <summary>
        /// Disposes the entire object.
        /// </summary>
        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }
}
