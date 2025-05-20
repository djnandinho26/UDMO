using MediatR;

namespace DigitalWorldOnline.Application.Admin.Commands
{
    public class DeleteAccountCommand : IRequest<Unit>
    {
        public long Id { get; set; }

        public DeleteAccountCommand(long id)
        {
            Id = id;
        }
    }
}