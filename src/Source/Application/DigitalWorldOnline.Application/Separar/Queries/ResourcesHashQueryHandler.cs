using DigitalWorldOnline.Commons.DTOs.Config;
using DigitalWorldOnline.Commons.Interfaces;
using MediatR;

namespace DigitalWorldOnline.Application.Separar.Queries
{
    public class ResourcesHashQueryHandler : IRequestHandler<ResourcesHashQuery, List<HashDTO>>
    {
        private readonly IServerQueriesRepository _repository;

        public ResourcesHashQueryHandler(IServerQueriesRepository repository)
        {
            _repository = repository;
        }

        public async Task<List<HashDTO> > Handle(ResourcesHashQuery request, CancellationToken cancellationToken)
        {
            return await _repository.GetResourcesHashAsync();
        }
    }
}