using MediatR;

namespace DigitalWorldOnline.Application.Separar.Commands.Delete
{
    public class DeleteGuildCommand : IRequest<Unit>
    {
        public long GuildId { get; private set; }

        public DeleteGuildCommand(long guildId)
        {
            GuildId = guildId;
        }
    }
}