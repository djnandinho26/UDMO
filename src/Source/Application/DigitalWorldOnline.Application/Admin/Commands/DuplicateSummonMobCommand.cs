using MediatR;

namespace DigitalWorldOnline.Application.Admin.Commands
{
    public class DuplicateSummonMobCommand : IRequest<Unit>
    {
        public long Id { get; set; }

        public DuplicateSummonMobCommand(long id)
        {
            Id = id;
        }
    }
}