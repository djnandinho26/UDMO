using MediatR;

namespace DigitalWorldOnline.Application.Admin.Commands
{
    public class DeleteEventMapMobsCommand : IRequest<Unit>
    {
        public long Id { get; set; }

        public DeleteEventMapMobsCommand(long id)
        {
            Id = id;
        }
    }
}