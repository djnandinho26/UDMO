﻿using DigitalWorldOnline.Commons.Models.Character;
using MediatR;

namespace DigitalWorldOnline.Application.Separar.Commands.Update
{
    public class UpdateCharacterDigimonArchiveItemCommand : IRequest<Unit>
    {
        public CharacterDigimonArchiveItemModel CharacterDigimonArchiveItem { get; }

        public UpdateCharacterDigimonArchiveItemCommand(CharacterDigimonArchiveItemModel characterDigimonArchiveItem)
        {
            CharacterDigimonArchiveItem = characterDigimonArchiveItem;
        }
    }
}