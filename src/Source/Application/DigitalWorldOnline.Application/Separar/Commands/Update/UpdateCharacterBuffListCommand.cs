﻿using DigitalWorldOnline.Commons.Models.Character;
using MediatR;

namespace DigitalWorldOnline.Application.Separar.Commands.Update
{
    public class UpdateCharacterBuffListCommand : IRequest<Unit>
    {
        public CharacterBuffListModel BuffList { get; set; }

        public UpdateCharacterBuffListCommand(CharacterBuffListModel buffList)
        {
            BuffList = buffList;
        }
    }
}