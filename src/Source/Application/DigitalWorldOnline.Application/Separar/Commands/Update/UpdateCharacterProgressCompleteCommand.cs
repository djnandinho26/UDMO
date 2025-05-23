﻿using DigitalWorldOnline.Commons.Models.Character;
using MediatR;

namespace DigitalWorldOnline.Application.Separar.Commands.Update
{
    public class UpdateCharacterProgressCompleteCommand : IRequest<Unit>
    {
        public CharacterProgressModel Progress { get; set; }

        public UpdateCharacterProgressCompleteCommand(CharacterProgressModel progress)
        {
            Progress = progress;
        }
    }
}