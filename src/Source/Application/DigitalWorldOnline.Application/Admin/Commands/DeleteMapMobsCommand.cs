using MediatR;

namespace DigitalWorldOnline.Application.Admin.Commands
{
    public class DeleteMapMobsCommand : IRequest<Unit>
    {
        public long Id { get; set; }

        public DeleteMapMobsCommand(long id)
        {
            Id = id;
        }
    }
}