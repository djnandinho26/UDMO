using MediatR;

namespace DigitalWorldOnline.Application.Admin.Commands
{
    public class DeleteScanConfigCommand : IRequest<Unit>
    {
        public long Id { get; set; }

        public DeleteScanConfigCommand(long id)
        {
            Id = id;
        }
    }
}