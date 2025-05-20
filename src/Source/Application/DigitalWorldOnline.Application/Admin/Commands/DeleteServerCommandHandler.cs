using DigitalWorldOnline.Commons.Repositories.Admin;
using MediatR;

namespace DigitalWorldOnline.Application.Admin.Commands
{
    public class DeleteServerCommandHandler : IRequestHandler<DeleteServerCommand,Unit>
    {
        private readonly IAdminCommandsRepository _repository;

        public DeleteServerCommandHandler(IAdminCommandsRepository repository)
        {
            _repository = repository;
        }

        public async Task<Unit> Handle(DeleteServerCommand request, CancellationToken cancellationToken)
        {
            await _repository.DeleteServerAsync(request.Id);

            return Unit.Value;
        }
    }
}