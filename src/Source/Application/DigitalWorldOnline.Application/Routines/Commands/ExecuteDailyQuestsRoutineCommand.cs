﻿using MediatR;

namespace DigitalWorldOnline.Application.Routines.Commands
{
    public class ExecuteDailyQuestsRoutineCommand : IRequest<Unit>
    {
        public List<short> QuestIdList { get; }

        public ExecuteDailyQuestsRoutineCommand(List<short> questIdList)
        {
            QuestIdList = questIdList;
        }
    }
}