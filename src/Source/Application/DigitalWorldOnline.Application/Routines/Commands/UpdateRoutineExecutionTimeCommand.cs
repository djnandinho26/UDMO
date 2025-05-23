﻿using MediatR;

namespace DigitalWorldOnline.Application.Routines.Commands
{
    public class UpdateRoutineExecutionTimeCommand : IRequest<Unit>
    {
        public long RoutineId { get; }

        public UpdateRoutineExecutionTimeCommand(long routineId)
        {
            RoutineId = routineId;
        }
    }
}