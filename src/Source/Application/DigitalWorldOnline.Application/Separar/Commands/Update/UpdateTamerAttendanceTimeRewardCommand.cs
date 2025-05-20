using DigitalWorldOnline.Commons.Models.Events;
using MediatR;

namespace DigitalWorldOnline.Application.Separar.Commands.Update
{
    public class UpdateTamerAttendanceTimeRewardCommand : IRequest<Unit>
    {
        public TimeRewardModel TimeRewardModel { get; set; }

        public UpdateTamerAttendanceTimeRewardCommand(TimeRewardModel timeReward)
        {
            TimeRewardModel = timeReward;
        }
    }
}
