using AutoMapper;
using DigitalWorldOnline.Application;
using DigitalWorldOnline.Application.Separar.Commands.Delete;
using DigitalWorldOnline.Application.Separar.Commands.Update;
using DigitalWorldOnline.Application.Separar.Queries;
using DigitalWorldOnline.Commons.Entities;
using DigitalWorldOnline.Commons.Enums.PacketProcessor;
using DigitalWorldOnline.Commons.Interfaces;
using DigitalWorldOnline.Commons.Models.Character;
using DigitalWorldOnline.Commons.Models.TamerShop;
using DigitalWorldOnline.Commons.Packets.PersonalShop;
using DigitalWorldOnline.GameHost;
using MediatR;

namespace DigitalWorldOnline.Game.PacketProcessors
{
    public class ConsignedShopViewPacketProcessor : IGamePacketProcessor
    {
        public GameServerPacketEnum Type => GameServerPacketEnum.ConsignedShopView;

        private readonly AssetsLoader _assets;
        private readonly MapServer _mapServer;
        private readonly ISender _sender;
        private readonly IMapper _mapper;

        public ConsignedShopViewPacketProcessor(
            MapServer mapServer,
            AssetsLoader assets,
            IMapper mapper,
            ISender sender)
        {
            _mapServer = mapServer;
            _assets = assets;
            _mapper = mapper;
            _sender = sender;
        }

        public async Task Process(GameClient client, byte[] packetData)
        {
            var packet = new GamePacketReader(packetData);

            Console.WriteLine($"Pacote de Visualização da Loja Consignada");

            packet.Skip(4);
            var handler = packet.ReadInt();

            Console.WriteLine($"Buscando loja consignada com o identificador {handler}...");
            var consignedShop = _mapper.Map<ConsignedShop>(await _sender.Send(new ConsignedShopByHandlerQuery(handler)));

            if (consignedShop == null)
            {
                Console.WriteLine($"Loja consignada não encontrada com o identificador {handler}.");
                client.Send(new ConsignedShopItemsViewPacket());

                Console.WriteLine($"Enviando pacote de descarregamento da loja consignada...");
                _mapServer.BroadcastForTamerViewsAndSelf(client.TamerId, new UnloadConsignedShopPacket(handler).Serialize());
                return;
            }

            Console.WriteLine($"Buscando proprietário da loja consignada com id {consignedShop.CharacterId}...");
            var shopOwner = _mapper.Map<CharacterModel>(await _sender.Send(new CharacterAndItemsByIdQuery(consignedShop.CharacterId)));

            if (shopOwner == null || shopOwner.ConsignedShopItems.Count == 0)
            {
                Console.WriteLine($"Excluindo loja consignada...");
                await _sender.Send(new DeleteConsignedShopCommand(handler));

                Console.WriteLine($"Enviando pacote de visualização dos itens da loja consignada...");
                client.Send(new ConsignedShopItemsViewPacket());

                Console.WriteLine($"Enviando pacote de descarregamento da loja consignada...");
                _mapServer.BroadcastForTamerViewsAndSelf(client.TamerId, new UnloadConsignedShopPacket(handler).Serialize());
                return;
            }

            foreach (var item in shopOwner.ConsignedShopItems.Items)
            {
                item.SetItemInfo(_assets.ItemInfo.FirstOrDefault(x => x.ItemId == item.ItemId));

                //TODO: generalizar isso em rotina
                if (item.ItemId > 0 && item.ItemInfo == null)
                {
                    item.SetItemId();
                    shopOwner.ConsignedShopItems.CheckEmptyItems();
                    Console.WriteLine($"Atualizando a lista de itens da loja consignada...");
                    await _sender.Send(new UpdateItemsCommand(shopOwner.ConsignedShopItems));
                }
            }

            Console.WriteLine($"Enviando pacote de visualização da lista de itens da loja consignada...");
            client.Send(new ConsignedShopItemsViewPacket(consignedShop, shopOwner.ConsignedShopItems, shopOwner.Name));
        }
    }
}
