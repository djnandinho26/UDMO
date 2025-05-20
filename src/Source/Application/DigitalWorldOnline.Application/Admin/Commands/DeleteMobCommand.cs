using MediatR;

namespace DigitalWorldOnline.Application.Admin.Commands
{
    public class DeleteMobCommand : IRequest<Unit>
    {
        public long Id { get; set; }

        public DeleteMobCommand(long id)
        {
            Id = id;
        }
    }
}