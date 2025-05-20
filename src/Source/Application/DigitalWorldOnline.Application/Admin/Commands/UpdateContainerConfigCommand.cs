using DigitalWorldOnline.Commons.DTOs.Assets;
using MediatR;

namespace DigitalWorldOnline.Application.Admin.Commands
{
    public class UpdateContainerConfigCommand : IRequest<Unit>
    {
        public ContainerAssetDTO Container { get; }

        public UpdateContainerConfigCommand(ContainerAssetDTO container)
        {
            Container = container;
        }
    }
}