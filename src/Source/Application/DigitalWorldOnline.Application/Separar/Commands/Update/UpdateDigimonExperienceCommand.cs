﻿using DigitalWorldOnline.Commons.Models.Digimon;
using MediatR;

namespace DigitalWorldOnline.Application.Separar.Commands.Update
{
    public class UpdateDigimonExperienceCommand : IRequest<Unit>
    {
        public DigimonModel Digimon { get; set; }

        public UpdateDigimonExperienceCommand(DigimonModel digimon)
        {
            Digimon = digimon;
        }
    }
}
