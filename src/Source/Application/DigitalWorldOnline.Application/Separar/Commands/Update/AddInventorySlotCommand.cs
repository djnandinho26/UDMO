using DigitalWorldOnline.Commons.Models.Base;
using MediatR;

namespace DigitalWorldOnline.Application.Separar.Commands.Update
{
    public class AddInventorySlotCommand : IRequest<Unit>
    {
        public ItemModel NewSlot { get; }

        public AddInventorySlotCommand(ItemModel newSlot)
        {
            NewSlot = newSlot;
        }
    }
}