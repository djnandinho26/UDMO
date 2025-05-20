using MediatR;

namespace DigitalWorldOnline.Application.Admin.Commands
{
    public class DeleteEventMapConfigCommand : IRequest<Unit>
    {
        public long Id { get; set; }

        public DeleteEventMapConfigCommand(long id)
        {
            Id = id;
        }
    }
}