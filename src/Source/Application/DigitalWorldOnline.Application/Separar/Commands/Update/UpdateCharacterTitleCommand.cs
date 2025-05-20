using MediatR;

namespace DigitalWorldOnline.Application.Separar.Commands.Update
{
    public class UpdateCharacterTitleCommand : IRequest<Unit>
    {
        public long CharacterId { get; set; }

        public short TitleId { get; set; }

        public UpdateCharacterTitleCommand(long characterId,
            short titleId)
        {
            CharacterId = characterId;
            TitleId = titleId;
        }
    }
}
