using DigitalWorldOnline.Commons.Models.Digimon;
using MediatR;

namespace DigitalWorldOnline.Application.Separar.Commands.Update
{
    public class UpdateDigimonBuffListCommand : IRequest<Unit>
    {
        public DigimonBuffListModel BuffList { get; set; }

        public UpdateDigimonBuffListCommand(DigimonBuffListModel buffList)
        {
            BuffList = buffList;
        }
    }
}