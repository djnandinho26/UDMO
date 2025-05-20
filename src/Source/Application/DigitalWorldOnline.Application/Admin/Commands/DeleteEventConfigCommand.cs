using MediatR;

namespace DigitalWorldOnline.Application.Admin.Commands
{
    public class DeleteEventConfigCommand : IRequest<Unit>
    {
        public long Id { get; set; }

        public DeleteEventConfigCommand(long id)
        {
            Id = id;
        }
    }
}