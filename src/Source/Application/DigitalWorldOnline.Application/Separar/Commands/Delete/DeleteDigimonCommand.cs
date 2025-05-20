using MediatR;

namespace DigitalWorldOnline.Application.Separar.Commands.Delete
{
    public class DeleteDigimonCommand : IRequest<Unit>
    {
        public long DigimonId { get; set; }

        public DeleteDigimonCommand(long digimonId)
        {
            DigimonId = digimonId;
        }
    }
}
