using DigitalWorldOnline.Commons.Enums.Character;
using MediatR;

namespace DigitalWorldOnline.Application.Separar.Commands.Delete
{
    public class DeleteCharacterCommand : IRequest<DeleteCharacterResultEnum>
    {
        public long AccountId { get; set; }

        public long CharacterId { get; set; }

        public DeleteCharacterCommand(long accountId, long characterId)
        {
            AccountId = accountId;
            CharacterId = characterId;
        }
    }
}
