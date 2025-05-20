using DigitalWorldOnline.Commons.Interfaces;
using MediatR;

namespace DigitalWorldOnline.Application.Separar.Commands.Update
{
    public class UpdateGuildMemberAuthorityCommandHandler : IRequestHandler<UpdateGuildMemberAuthorityCommand,Unit>
    {
        private readonly IServerCommandsRepository _repository;

        public UpdateGuildMemberAuthorityCommandHandler(IServerCommandsRepository repository)
        {
            _repository = repository;
        }

        public async Task<Unit> Handle(UpdateGuildMemberAuthorityCommand request, CancellationToken cancellationToken)
        {
            await _repository.UpdateGuildMemberAuthorityAsync(request.GuildMember);

            return Unit.Value;
        }
    }
}