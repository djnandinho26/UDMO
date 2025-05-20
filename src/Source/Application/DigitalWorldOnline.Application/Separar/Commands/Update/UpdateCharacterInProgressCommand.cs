using DigitalWorldOnline.Commons.Models.Character;
using MediatR;

namespace DigitalWorldOnline.Application.Separar.Commands.Update
{
    public class UpdateCharacterInProgressCommand : IRequest<Unit>
    {
        public InProgressQuestModel Progress { get; set; }

        public UpdateCharacterInProgressCommand(InProgressQuestModel progress)
        {
            Progress = progress;
        }
    }
}