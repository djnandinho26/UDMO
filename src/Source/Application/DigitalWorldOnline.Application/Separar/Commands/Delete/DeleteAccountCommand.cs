using MediatR;

namespace DigitalWorldOnline.Application.Separar.Commands.Delete
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
