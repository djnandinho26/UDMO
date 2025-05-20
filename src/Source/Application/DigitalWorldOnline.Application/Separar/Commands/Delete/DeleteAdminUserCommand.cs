using DigitalWorldOnline.Commons.Models.Config;
using MediatR;

namespace DigitalWorldOnline.Application.Separar.Commands.Delete
{
    public class DeleteAdminUserCommand : IRequest<Unit>
    {
        public long UserId { get; private set; }

        public DeleteAdminUserCommand(long userId)
        {
            UserId = userId;
        }
    }
}