using MediatR;

namespace DigitalWorldOnline.Application.Admin.Commands
{
    public class DeleteEventMobCommand : IRequest<Unit>
    {
        public long Id { get; set; }

        public DeleteEventMobCommand(long id)
        {
            Id = id;
        }
    }
}