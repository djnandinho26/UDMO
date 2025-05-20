using DigitalWorldOnline.Commons.Models.Config;
using MediatR;

namespace DigitalWorldOnline.Application.Separar.Commands.Update
{
    public class UpdateMapConfigCommand : IRequest<Unit>
    {
        public MapConfigModel MapConfig { get; set; }

        public UpdateMapConfigCommand(MapConfigModel mapConfig)
        {
            MapConfig = mapConfig;
        }
    }
}
