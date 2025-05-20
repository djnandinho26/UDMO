using MediatR;

namespace DigitalWorldOnline.Application.Admin.Commands
{
    public class DuplicateEventMobCommand : IRequest<Unit>
    {
        public long Id { get; set; }

        public DuplicateEventMobCommand(long id)
        {
            Id = id;
        }
    }
}