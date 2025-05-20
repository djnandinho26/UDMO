using MediatR;

namespace DigitalWorldOnline.Application.Admin.Commands
{
    public class DeleteContainerConfigCommand : IRequest<Unit>
    {
        public long Id { get; set; }

        public DeleteContainerConfigCommand(long id)
        {
            Id = id;
        }
    }
}