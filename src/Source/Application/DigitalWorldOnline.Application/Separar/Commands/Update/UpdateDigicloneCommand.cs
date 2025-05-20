using DigitalWorldOnline.Commons.Models.Digimon;
using MediatR;

namespace DigitalWorldOnline.Application.Separar.Commands.Update
{
    public class UpdateDigicloneCommand : IRequest<Unit>
    {
        public DigimonDigicloneModel Digiclone { get; set; }

        public UpdateDigicloneCommand(DigimonDigicloneModel digiclone)
        {
            Digiclone = digiclone;
        }
    }
}