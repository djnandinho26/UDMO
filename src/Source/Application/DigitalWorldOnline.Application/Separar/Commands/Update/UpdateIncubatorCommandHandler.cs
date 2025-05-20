using DigitalWorldOnline.Commons.Interfaces;
using MediatR;

namespace DigitalWorldOnline.Application.Separar.Commands.Update
{
    public class UpdateIncubatorCommandHandler : IRequestHandler<UpdateIncubatorCommand,Unit>
    {
        private readonly ICharacterCommandsRepository _repository;

        public UpdateIncubatorCommandHandler(ICharacterCommandsRepository repository)
        {
            _repository = repository;
        }

        public async Task<Unit> Handle(UpdateIncubatorCommand request, CancellationToken cancellationToken)
        {
            await _repository.UpdateIncubatorAsync(request.Incubator);

            return Unit.Value;
        }
    }
}