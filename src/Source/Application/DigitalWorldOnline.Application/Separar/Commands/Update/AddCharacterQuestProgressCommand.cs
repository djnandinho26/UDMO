using DigitalWorldOnline.Commons.Models.Character;
using MediatR;

namespace DigitalWorldOnline.Application.Separar.Commands.Update
{
    public class AddCharacterProgressCommand : IRequest<Unit>
    {
        public CharacterProgressModel Progress { get; set; }

        public AddCharacterProgressCommand(CharacterProgressModel progress)
        {
            Progress = progress;
        }
    }
}