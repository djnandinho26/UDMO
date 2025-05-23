﻿using DigitalWorldOnline.Commons.DTOs.Config;
using MediatR;

namespace DigitalWorldOnline.Application.Admin.Commands
{
    public class UpdateHatchConfigCommand : IRequest<Unit>
    {
        public HatchConfigDTO Hatch { get; }

        public UpdateHatchConfigCommand(HatchConfigDTO hatch)
        {
            Hatch = hatch;
        }
    }
}