using DigitalWorldOnline.Commons.Interfaces;
using MediatR;

namespace DigitalWorldOnline.Application.Separar.Commands.Update
{
    public class RemoveActiveQuestCommandHandler : IRequestHandler<RemoveActiveQuestCommand,Unit>
    {
        private readonly IAccountCommandsRepository _repository;

        public RemoveActiveQuestCommandHandler(IAccountCommandsRepository repository)
        {
            _repository = repository;
        }

        public async Task<Unit> Handle(RemoveActiveQuestCommand request, CancellationToken cancellationToken)
        {
            await _repository.RemoveActiveQuestAsync(request.ProgressQuestId);

            return Unit.Value;
        }
    }
}