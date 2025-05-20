using MediatR;

namespace DigitalWorldOnline.Application.Separar.Commands.Delete
{
    public class DeleteGuildMemberCommand : IRequest<Unit>
    {
        public long CharacterId { get; private set; }
        public long GuildId { get; private set; }

        public DeleteGuildMemberCommand(long characterId, long guildId)
        {
            GuildId = guildId;
            CharacterId = characterId;
        }
    }
}