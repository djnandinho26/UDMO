using DigitalWorldOnline.Commons.DTOs.Assets;
using MediatR;

namespace DigitalWorldOnline.Application.Admin.Commands
{
    public class UpdateSpawnPointCommand : IRequest<Unit>
    {
        public long MapId { get; }
        public MapRegionAssetDTO SpawnPoint { get; }

        public UpdateSpawnPointCommand(MapRegionAssetDTO spawnPoint, long mapId)
        {
            SpawnPoint = spawnPoint;
            MapId = mapId;
        }
    }
}