using DigitalWorldOnline.Commons.Models.Digimon;
using MediatR;

namespace DigitalWorldOnline.Application.Separar.Commands.Update
{
    public class UpdatePartnerCurrentTypeCommand : IRequest<Unit>
    {
        public DigimonModel Digimon { get; set; }

        public UpdatePartnerCurrentTypeCommand(DigimonModel digimon)
        {
            Digimon = digimon;
        }
    }
}