using DigitalWorldOnline.Commons.Models.Base;
using MediatR;

namespace DigitalWorldOnline.Application.Separar.Commands.Update
{
    public class UpdateItemSocketStatusCommand : IRequest<Unit>
    {
        public ItemModel Item { get; }

        public UpdateItemSocketStatusCommand(ItemModel item)
        {
            Item = item;
        }
    }
}