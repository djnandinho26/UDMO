using DigitalWorldOnline.Commons.DTOs.Assets;
using MediatR;

namespace DigitalWorldOnline.Application.Admin.Commands
{
    public class UpdateScanConfigCommand : IRequest<Unit>
    {
        public ScanDetailAssetDTO Scan { get; }

        public UpdateScanConfigCommand(ScanDetailAssetDTO scan)
        {
            Scan = scan;
        }
    }
}