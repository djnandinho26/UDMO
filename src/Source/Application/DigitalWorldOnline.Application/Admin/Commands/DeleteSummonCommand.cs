using MediatR;

namespace DigitalWorldOnline.Application.Admin.Commands
{
    public class DeleteSummonCommand : IRequest<Unit>
    {
        public long Id { get; set; }

        public DeleteSummonCommand(long id)
        {
            Id = id;
        }
    }
}