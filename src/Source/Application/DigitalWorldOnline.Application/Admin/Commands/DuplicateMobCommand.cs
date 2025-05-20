using MediatR;

namespace DigitalWorldOnline.Application.Admin.Commands
{
    public class DuplicateMobCommand : IRequest<Unit>
    {
        public long Id { get; set; }

        public DuplicateMobCommand(long id)
        {
            Id = id;
        }
    }
}