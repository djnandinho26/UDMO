using MediatR;

namespace DigitalWorldOnline.Application.Separar.Commands.Delete
{
    public class DeleteMapConfigCommand : IRequest<Unit>
    {
        public long Id { get; set; }

        public DeleteMapConfigCommand(long id)
        {
            Id = id;
        }
    }
}