using DigitalWorldOnline.Commons.Models.Events;
using MediatR;

namespace DigitalWorldOnline.Application.Separar.Commands.Update
{
    public class UpdateTamerTimeRewardCommand : IRequest<Unit>
    {

        public TimeRewardModel TimeRewardModel { get; set; }

        public UpdateTamerTimeRewardCommand(TimeRewardModel timeReward)
        {
            TimeRewardModel = timeReward;
        }
    }
}
