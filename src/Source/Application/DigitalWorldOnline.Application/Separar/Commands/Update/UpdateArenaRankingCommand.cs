
using DigitalWorldOnline.Commons.Models.Mechanics;
using MediatR;

namespace DigitalWorldOnline.Application.Separar.Commands.Update
{
    public class UpdateArenaRankingCommand : IRequest<Unit>
    {
        public ArenaRankingModel Arena { get; private set; }

        public UpdateArenaRankingCommand(ArenaRankingModel arena)
        {
            Arena = arena;
        }
    }
}