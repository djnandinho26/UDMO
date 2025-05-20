using DigitalWorldOnline.Commons.Repositories.Admin;
using MediatR;

namespace DigitalWorldOnline.Application.Admin.Commands
{
    public class DeleteUserCommandHandler : IRequestHandler<DeleteUserCommand,Unit>
    {
        private readonly IAdminCommandsRepository _repository;

        public DeleteUserCommandHandler(IAdminCommandsRepository repository)
        {
            _repository = repository;
        }

        public async Task<Unit> Handle(DeleteUserCommand request, CancellationToken cancellationToken)
        {
            await _repository.DeleteUserAsync(request.Id);

            return Unit.Value;
        }
    }
}