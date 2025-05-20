using MediatR;

namespace DigitalWorldOnline.Application.Admin.Commands
{
    public class DeleteUserCommand : IRequest<Unit>
    {
        public long Id { get; set; }

        public DeleteUserCommand(long id)
        {
            Id = id;
        }
    }
}