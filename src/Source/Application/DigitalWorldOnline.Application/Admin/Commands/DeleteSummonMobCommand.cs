using MediatR;

namespace DigitalWorldOnline.Application.Admin.Commands
{
    public class DeleteSummonMobCommand : IRequest<Unit>
    {
        public long Id { get; set; }

        public DeleteSummonMobCommand(long id)
        {
            Id = id;
        }
    }
}