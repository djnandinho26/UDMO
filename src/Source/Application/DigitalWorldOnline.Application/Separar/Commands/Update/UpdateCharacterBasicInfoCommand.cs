﻿using DigitalWorldOnline.Commons.Models.Character;
using MediatR;

namespace DigitalWorldOnline.Application.Separar.Commands.Update
{
    public class UpdateCharacterBasicInfoCommand : IRequest<Unit>
    {
        public CharacterModel Character { get; private set; }

        public UpdateCharacterBasicInfoCommand(CharacterModel character)
        {
            Character = character;
        }
    }
}