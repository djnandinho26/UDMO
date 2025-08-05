using DigitalWorldOnline.Commons.Enums;
using DigitalWorldOnline.Commons.Enums.ClientEnums;
using DigitalWorldOnline.Commons.Models.Character;
using DigitalWorldOnline.Commons.Packets.Chat;
using DigitalWorldOnline.Commons.Packets.Items;
using System.Text;

namespace DigitalWorldOnline.Commons.Models.Base
{
    /// <summary>
    /// Classe parcial que define comportamentos e operações para listas de itens.
    /// Fornece funcionalidades para gerenciar inventários, adicionar, remover e organizar itens.
    /// </summary>
    public partial class ItemListModel
    {
        /// <summary>
        /// Retorna a quantidade atual de itens no inventário (excluindo slots vazios).
        /// </summary>
        public byte Count => (byte)Items.Count(x => x.ItemId != 0);

        /// <summary>
        /// Retorna a quantidade atual de slots vazios disponíveis.
        /// </summary>
        public byte TotalEmptySlots => (byte)Items.Count(x => x.ItemId == 0);

        /// <summary>
        /// Indica se o inventário pode ser recuperado (100 se tem itens ou bits, 0 caso contrário).
        /// </summary>
        public int RetrieveEnabled => Count > 0 || Bits > 0 ? 100 : 0;

        /// <summary>
        /// Organiza os itens por tipo, ID e quantidade.
        /// Itens válidos ficam no início, slots vazios no final.
        /// </summary>
        public void Sort()
        {
            // Separa itens existentes e os ordena por tipo, ID e quantidade
            var existingItens = Items
                .Where(x => x.ItemId > 0)
                .OrderByDescending(x => x.ItemInfo.Type)
                .ThenByDescending(x => x.ItemId)
                .ThenByDescending(x => x.Amount)
                .ToList();

            // Adiciona slots vazios ao final
            var emptyItens = Items
                .Where(x => x.ItemId == 0)
                .ToList();

            existingItens.AddRange(emptyItens);

            // Reorganiza os slots sequencialmente
            var slot = 0;
            foreach (var existingItem in existingItens)
            {
                existingItem.Slot = slot;
                slot++;
            }

            Items = existingItens;
        }

        /// <summary>
        /// Aumenta o tamanho atual do inventário adicionando novos slots.
        /// </summary>
        /// <param name="amount">Quantidade de slots a adicionar (padrão: 1)</param>
        /// <returns>Novo tamanho total do inventário</returns>
        public byte AddSlots(byte amount = 1)
        {
            for (byte i = 0; i < amount; i++)
            {
                var newItemSlot = new ItemModel(Items.Max(x => x.Slot))
                {
                    ItemListId = Id
                };

                Items.Add(newItemSlot);
                Size++;
            }

            return Size;
        }

        /// <summary>
        /// Adiciona um único slot ao inventário.
        /// </summary>
        /// <returns>O novo slot criado</returns>
        public ItemModel AddSlot()
        {
            var newItemSlot = new ItemModel(Items.Max(x => x.Slot))
            {
                ItemListId = Id
            };

            Items.Add(newItemSlot);
            Size++;

            return newItemSlot;
        }

        /// <summary>
        /// Conta a quantidade total de itens por ID específico.
        /// </summary>
        /// <param name="itemId">ID do item a ser contado</param>
        /// <returns>Quantidade total do item</returns>
        public int CountItensById(int itemId)
        {
            var total = 0;
            var items = FindItemsById(itemId);
            foreach (var targetItem in items)
            {
                total += targetItem.Amount;
            }

            return total;
        }

        /// <summary>
        /// Remove ou reduz itens de uma seção específica.
        /// Utiliza sistema de backup para reverter em caso de falha.
        /// </summary>
        /// <param name="itemSection">Seção do item</param>
        /// <param name="totalAmount">Quantidade total a ser removida</param>
        /// <returns>True se bem-sucedido, False caso contrário</returns>
        public bool RemoveOrReduceItemsBySection(int itemSection, int totalAmount)
        {
            var backup = BackupOperation();

            var targetAmount = totalAmount;
            var targetItems = FindItemsBySection(itemSection);
            targetItems = targetItems.OrderBy(x => x.Slot).ToList();

            foreach (var targetItem in targetItems)
            {
                if (targetItem.Amount >= targetAmount)
                {
                    targetItem.ReduceAmount(targetAmount);
                    targetAmount = 0;
                }
                else
                {
                    targetAmount -= targetItem.Amount;
                    targetItem.SetAmount(); // Zera o item
                }

                if (targetAmount == 0)
                    break;
            }

            // Se ainda resta quantidade a remover, reverte a operação
            if (targetAmount > 0)
            {
                RevertOperation(backup);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Remove ou reduz itens por ID específico.
        /// Utiliza sistema de backup para reverter em caso de falha.
        /// </summary>
        /// <param name="itemId">ID do item</param>
        /// <param name="totalAmount">Quantidade total a ser removida</param>
        /// <returns>True se bem-sucedido, False caso contrário</returns>
        public bool RemoveOrReduceItemsByItemId(int itemId, int totalAmount)
        {
            var backup = BackupOperation();

            var targetAmount = totalAmount;
            var targetItems = FindItemsById(itemId);
            targetItems = targetItems.OrderBy(x => x.Slot).ToList();

            foreach (var targetItem in targetItems)
            {
                if (targetItem.Amount >= targetAmount)
                {
                    targetItem.ReduceAmount(targetAmount);
                    targetAmount = 0;
                }
                else
                {
                    targetAmount -= targetItem.Amount;
                    targetItem.SetAmount(); // Zera o item
                }

                if (targetAmount == 0)
                    break;
            }

            // Se ainda resta quantidade a remover, reverte a operação
            if (targetAmount > 0)
            {
                RevertOperation(backup);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Encontra todos os itens de uma seção específica.
        /// </summary>
        /// <param name="itemSection">Seção do item</param>
        /// <returns>Lista de itens da seção especificada</returns>
        public List<ItemModel> FindItemsBySection(int itemSection)
        {
            return Items
                .Where(x => x.Amount > 0 && x.ItemInfo?.Section == itemSection)
                .ToList();
        }

        /// <summary>
        /// Encontra o primeiro item de uma seção específica.
        /// </summary>
        /// <param name="itemSection">Seção do item</param>
        /// <returns>Primeiro item encontrado ou null</returns>
        public ItemModel? FindItemBySection(int itemSection)
        {
            return Items.FirstOrDefault(x => x.Amount > 0 && x.ItemInfo?.Section == itemSection);
        }

        /// <summary>
        /// Encontra um item por ID específico.
        /// </summary>
        /// <param name="itemId">ID do item</param>
        /// <param name="allowEmpty">Se permite itens com quantidade zero</param>
        /// <returns>Item encontrado ou null</returns>
        public ItemModel? FindItemById(int itemId, bool allowEmpty = false)
        {
            if (allowEmpty)
                return Items.FirstOrDefault(x => itemId == x.ItemId);
            else
                return Items.FirstOrDefault(x => x.Amount > 0 && itemId == x.ItemId);
        }

        /// <summary>
        /// Encontra um slot disponível para um item específico.
        /// Verifica primeiro se pode empilhar com item existente.
        /// </summary>
        /// <param name="targetItem">Item alvo</param>
        /// <returns>Índice do slot disponível</returns>
        public int FindAvailableSlot(ItemModel targetItem)
        {
            // Tenta encontrar slot com mesmo item que pode empilhar
            var slot = Items.FindIndex(x =>
                x.ItemId == targetItem.ItemId &&
                x.Amount + targetItem.Amount < targetItem.ItemInfo.Overlap);

            // Se não encontrou, pega slot vazio
            if (slot < 0)
                slot = GetEmptySlot;

            return slot;
        }

        /// <summary>
        /// Encontra todos os itens com ID específico.
        /// </summary>
        /// <param name="itemId">ID do item</param>
        /// <param name="allowEmpty">Se permite itens com quantidade zero</param>
        /// <returns>Lista de itens encontrados</returns>
        public List<ItemModel> FindItemsById(int itemId, bool allowEmpty = false)
        {
            if (allowEmpty)
                return Items.Where(x => itemId == x.ItemId).ToList();
            else
                return Items.Where(x => x.Amount > 0 && itemId == x.ItemId).ToList();
        }

        /// <summary>
        /// Encontra um item por slot específico.
        /// </summary>
        /// <param name="slot">Número do slot</param>
        /// <returns>Item encontrado ou null se slot inválido</returns>
        public ItemModel FindItemBySlot(int slot)
        {
            if (slot < 0) return null;

            var ItemInfo = Items.First(x => x.Slot == slot);

            return ItemInfo;
        }

        /// <summary>
        /// Encontra um item por slot de troca específico.
        /// </summary>
        /// <param name="slot">Número do slot de troca</param>
        /// <returns>Item encontrado ou null se slot inválido</returns>
        public ItemModel FindItemByTradeSlot(int slot)
        {
            if (slot < 0) return null;

            var ItemInfo = Items.First(x => x.TradeSlot == slot);

            return ItemInfo;
        }

        /// <summary>
        /// Encontra um item válido por slot (para sistema de presentes).
        /// Só retorna itens com ItemId > 0.
        /// </summary>
        /// <param name="slot">Número do slot</param>
        /// <returns>Item encontrado ou null</returns>
        public ItemModel GiftFindItemBySlot(int slot)
        {
            if (slot < 0) return null;

            var ItemInfo = Items.FirstOrDefault(x => x.Slot == slot && x.ItemId > 0);

            if (ItemInfo == null)
                return null;

            return ItemInfo;
        }

        /// <summary>
        /// Atualiza os slots de presentes reorganizando itens válidos.
        /// Remove itens vazios e reorganiza os restantes sequencialmente.
        /// </summary>
        /// <returns>True se operação bem-sucedida, False se não há itens</returns>
        public bool UpdateGiftSlot()
        {
            var ItemInfo = Items.Where(x => x.ItemId > 0).ToList();

            if (ItemInfo.Count <= 0)
                return false;

            var slot = -1;

            foreach (var item in ItemInfo)
            {
                slot++;

                // Cria novo item com dados do original
                var newItem = new ItemModel();
                newItem.SetItemId(item.ItemId);
                newItem.SetAmount(item.Amount);
                newItem.SetItemInfo(item.ItemInfo);

                // Remove item original e adiciona o novo
                RemoveItem(item, (short)item.Slot);
                AddItem(newItem);
            }

            return true;
        }

        /// <summary>
        /// Retorna o índice do primeiro slot vazio ou -1 se não houver.
        /// </summary>
        public int GetEmptySlot => Items.FindIndex(x => x.ItemId == 0);

        /// <summary>
        /// Insere um item em um slot vazio específico.
        /// </summary>
        /// <param name="newItem">Item a ser inserido</param>
        /// <returns>Slot onde o item foi inserido</returns>
        public int InsertItem(ItemModel newItem)
        {
            var targetSlot = GetEmptySlot;
            newItem.Id = Items[targetSlot].Id;
            newItem.Slot = targetSlot;

            Items[targetSlot] = newItem;

            return targetSlot;
        }

        /// <summary>
        /// Adiciona bits ao inventário com proteção contra overflow.
        /// </summary>
        /// <param name="bits">Quantidade de bits a adicionar</param>
        /// <returns>True se adicionado com sucesso, False se atingiu limite máximo</returns>
        public bool AddBits(long bits)
        {
            if (Bits + bits > long.MaxValue)
            {
                Bits = long.MaxValue;
                return false;
            }
            else
            {
                Bits += bits;
                return true;
            }
        }

        /// <summary>
        /// Remove bits do inventário com proteção contra valores negativos.
        /// </summary>
        /// <param name="bits">Quantidade de bits a remover</param>
        /// <returns>True se removido com sucesso, False se não há bits suficientes</returns>
        public bool RemoveBits(long bits)
        {
            if (Bits >= bits)
            {
                Bits -= bits;
                return true;
            }
            else
            {
                Bits = 0;
                return false;
            }
        }

        /// <summary>
        /// Adiciona múltiplos itens ao inventário com sistema de backup.
        /// </summary>
        /// <param name="itemsToAdd">Lista de itens a adicionar</param>
        /// <param name="isShop">Se é operação de loja (ignora limite de empilhamento)</param>
        /// <returns>True se todos os itens foram adicionados, False caso contrário</returns>
        public bool AddItems(List<ItemModel> itemsToAdd, bool isShop = false)
        {
            var backup = BackupOperation();

            foreach (var itemToAdd in itemsToAdd)
            {
                // Limpa referências para evitar conflitos
                itemToAdd.ItemList = null;
                itemToAdd.ItemListId = 0;

                if (itemToAdd.Amount == 0 || itemToAdd.ItemId == 0)
                    continue;

                // Tenta preencher slots existentes primeiro
                FillExistentSlots(itemToAdd, isShop);
                // Depois adiciona novos slots se necessário
                AddNewSlots(itemToAdd, isShop);

                // Se ainda sobrou quantidade, falha na operação
                if (itemToAdd.Amount > 0)
                {
                    RevertOperation(backup);
                    return false;
                }
            }

            CheckEmptyItems();
            return true;
        }

        /// <summary>
        /// Adiciona item ao armazenamento de presentes.
        /// Considera limite de empilhamento e cria novos slots conforme necessário.
        /// </summary>
        /// <param name="newItem">Item a ser adicionado</param>
        /// <returns>True se adicionado com sucesso</returns>
        public bool AddItemGiftStorage(ItemModel newItem)
        {
            if (newItem.Amount <= 0 || newItem.ItemId == 0)
                return false;

            var itemToAdd = (ItemModel)newItem.Clone();

            // Adiciona item até que toda quantidade seja alocada ou não haja mais espaço
            while (itemToAdd.Amount > 0 && Count < Size)
            {
                var targetSlot = GetEmptySlot;

                if (targetSlot == -1)
                    break;

                itemToAdd.Slot = targetSlot;
                var newClon = (ItemModel)itemToAdd.Clone();

                // Verifica limite de empilhamento
                if (itemToAdd.Amount > itemToAdd.ItemInfo.Overlap)
                {
                    newClon.SetAmount(itemToAdd.ItemInfo.Overlap);
                    itemToAdd.SetAmount(itemToAdd.Amount - itemToAdd.ItemInfo.Overlap);
                }
                else
                {
                    newClon.SetAmount(itemToAdd.Amount);
                    itemToAdd.SetAmount(); // Zera
                }

                newClon.Id = Items[targetSlot].Id;
                newClon.Slot = targetSlot;

                Items[targetSlot] = newClon;
            }

            CheckEmptyItems();
            newItem.Slot = itemToAdd.Slot;

            return true;
        }

        /// <summary>
        /// Adiciona um item ao inventário com sistema de backup.
        /// Tenta preencher slots existentes antes de criar novos.
        /// </summary>
        /// <param name="newItem">Item a ser adicionado</param>
        /// <returns>True se adicionado com sucesso, False caso contrário</returns>
        public bool AddItem(ItemModel newItem)
        {
            if (newItem.Amount == 0 || newItem.ItemId == 0)
                return false;

            var backup = BackupOperation();
            var itemToAdd = (ItemModel)newItem.Clone();

            FillExistentSlots(itemToAdd);
            AddNewSlots(itemToAdd);

            // Se ainda sobrou quantidade, reverte operação
            if (itemToAdd.Amount > 0)
            {
                RevertOperation(backup);
                return false;
            }

            CheckEmptyItems();
            newItem.Slot = itemToAdd.Slot;

            return true;
        }

        /// <summary>
        /// Adiciona item para sistema de troca (não preenche slots existentes).
        /// </summary>
        /// <param name="newItem">Item a ser adicionado</param>
        /// <returns>True se adicionado com sucesso, False caso contrário</returns>
        public bool AddItemTrade(ItemModel newItem)
        {
            if (newItem.Amount == 0 || newItem.ItemId == 0)
                return false;

            var backup = BackupOperation();
            var itemToAdd = (ItemModel)newItem.Clone();

            // Só adiciona novos slots, não preenche existentes
            AddNewSlots(itemToAdd);

            if (itemToAdd.Amount > 0)
            {
                RevertOperation(backup);
                return false;
            }

            CheckEmptyItems();
            newItem.Slot = itemToAdd.Slot;

            return true;
        }

        /// <summary>
        /// Adiciona item a um slot específico.
        /// Copia todas as propriedades do item para o slot alvo.
        /// </summary>
        /// <param name="itemToAdd">Item a ser adicionado</param>
        /// <param name="slot">Slot específico</param>
        /// <returns>True se adicionado com sucesso</returns>
        public bool AddItemWithSlot(ItemModel itemToAdd, int slot)
        {
            if (itemToAdd.Amount == 0 || itemToAdd.ItemId == 0)
                return false;

            var tempItem = (ItemModel)itemToAdd.Clone();
            var targetSlot = FindItemBySlot(slot);

            // Copia todas as propriedades
            targetSlot.ItemId = tempItem.ItemId;
            targetSlot.Amount = tempItem.Amount;
            targetSlot.Power = tempItem.Power;
            targetSlot.RerollLeft = tempItem.RerollLeft;
            targetSlot.FamilyType = tempItem.FamilyType;
            targetSlot.Duration = tempItem.Duration;
            targetSlot.EndDate = tempItem.EndDate;
            targetSlot.FirstExpired = tempItem.FirstExpired;
            targetSlot.AccessoryStatus = tempItem.AccessoryStatus;
            targetSlot.SocketStatus = tempItem.SocketStatus;
            targetSlot.ItemInfo = tempItem.ItemInfo;

            return true;
        }

        /// <summary>
        /// Divide item adicionando quantidade a um slot específico.
        /// </summary>
        /// <param name="itemToAdd">Item a ser dividido</param>
        /// <param name="targetSlot">Slot alvo</param>
        /// <returns>True se dividido com sucesso</returns>
        public bool SplitItem(ItemModel itemToAdd, int targetSlot)
        {
            if (itemToAdd == null || itemToAdd.Amount == 0 || itemToAdd.ItemId == 0)
                return false;

            FillExistentSlot(itemToAdd, targetSlot);
            CheckEmptyItems();

            return true;
        }

        /// <summary>
        /// Cria backup da lista atual de itens para possível reversão.
        /// </summary>
        /// <returns>Lista de backup dos itens</returns>
        private List<ItemModel> BackupOperation()
        {
            var backup = new List<ItemModel>();
            backup.AddRange(Items);
            return backup;
        }

        /// <summary>
        /// Reverte operação usando backup fornecido.
        /// </summary>
        /// <param name="backup">Lista de backup para restaurar</param>
        private void RevertOperation(List<ItemModel> backup)
        {
            Items.Clear();
            Items.AddRange(backup);
            CheckEmptyItems();
        }

        /// <summary>
        /// Adiciona item a novos slots vazios respeitando limite de empilhamento.
        /// </summary>
        /// <param name="itemToAdd">Item a ser adicionado</param>
        /// <param name="isShop">Se é operação de loja (ignora limite de empilhamento)</param>
        private void AddNewSlots(ItemModel itemToAdd, bool isShop = false)
        {
            while (itemToAdd.Amount > 0 && Count < Size)
            {
                itemToAdd.Slot = GetEmptySlot;
                var newItem = (ItemModel)itemToAdd.Clone();

                // Verifica limite de empilhamento se não for loja
                if (itemToAdd.Amount > itemToAdd.ItemInfo.Overlap && !isShop)
                {
                    itemToAdd.ReduceAmount(itemToAdd.ItemInfo.Overlap);
                    newItem.SetAmount(itemToAdd.ItemInfo.Overlap);
                }
                else
                {
                    newItem.SetAmount(itemToAdd.Amount);
                    itemToAdd.SetAmount(); // Zera
                }

                InsertItem(newItem);
            }
        }

        /// <summary>
        /// Método interno para verificar itens expirados.
        /// Atualmente não implementado.
        /// </summary>
        internal void CheckExpiredItems()
        {
            // TODO: Implementar verificação de itens expirados
        }

        /// <summary>
        /// Preenche slots existentes com o mesmo item respeitando limite de empilhamento.
        /// </summary>
        /// <param name="itemToAdd">Item a ser adicionado</param>
        /// <param name="isShop">Se é operação de loja (ignora limite de empilhamento)</param>
        private void FillExistentSlots(ItemModel itemToAdd, bool isShop = false)
        {
            var targetItems = FindItemsById(itemToAdd.ItemId);

            // Só preenche itens que podem empilhar (Overlap > 1)
            foreach (var targetItem in targetItems.Where(x => x.ItemInfo.Overlap > 1))
            {
                if (targetItem.Amount + itemToAdd.Amount > itemToAdd.ItemInfo.Overlap && !isShop)
                {
                    // Adiciona até o limite máximo
                    itemToAdd.ReduceAmount(itemToAdd.ItemInfo.Overlap - targetItem.Amount);
                    targetItem.SetAmount(itemToAdd.ItemInfo.Overlap);
                }
                else
                {
                    // Adiciona toda a quantidade
                    targetItem.IncreaseAmount(itemToAdd.Amount);
                    itemToAdd.SetAmount(); // Zera
                }

                itemToAdd.Slot = targetItem.Slot;
            }
        }

        /// <summary>
        /// Preenche um slot específico com item.
        /// </summary>
        /// <param name="itemToAdd">Item a ser adicionado</param>
        /// <param name="targetSlot">Slot alvo específico</param>
        private void FillExistentSlot(ItemModel itemToAdd, int targetSlot)
        {
            var targetItem = FindItemBySlot(targetSlot);

            // Só permite se for mesmo item ou slot vazio
            if (targetItem.ItemId == itemToAdd.ItemId || targetItem.ItemId == 0)
            {
                if (targetItem.Amount + itemToAdd.Amount > itemToAdd.ItemInfo.Overlap)
                {
                    // Adiciona até o limite
                    itemToAdd.IncreaseAmount(itemToAdd.ItemInfo.Overlap - targetItem.Amount);
                    targetItem.SetAmount(targetItem.ItemInfo.Overlap);
                }
                else
                {
                    // Adiciona toda a quantidade
                    targetItem.IncreaseAmount(itemToAdd.Amount);
                    itemToAdd.SetAmount(); // Zera
                }

                // Atualiza propriedades do slot
                targetItem.SetItemId(itemToAdd.ItemId);
                targetItem.SetItemInfo(itemToAdd.ItemInfo);
                targetItem.SetRemainingTime((uint)itemToAdd.ItemInfo.UsageTimeMinutes);
            }
        }

        /// <summary>
        /// Move item entre dois slots.
        /// Lida com empilhamento e troca de posições.
        /// </summary>
        /// <param name="originSlot">Slot de origem</param>
        /// <param name="destinationSlot">Slot de destino</param>
        /// <returns>True se movido com sucesso</returns>
        public bool MoveItem(short originSlot, short destinationSlot)
        {
            var originItem = FindItemBySlot(originSlot);
            var destinationItem = FindItemBySlot(destinationSlot);

            // Não permite mover slot vazio
            if (originItem.ItemId == 0)
                return false;

            // Se são o mesmo item, tenta empilhar
            if (originItem.ItemId == destinationItem.ItemId)
            {
                if (originItem.Amount + destinationItem.Amount > originItem.ItemInfo.Overlap)
                {
                    // Empilha até o limite
                    originItem.ReduceAmount(originItem.ItemInfo.Overlap - destinationItem.Amount);
                    destinationItem.SetAmount(originItem.ItemInfo.Overlap);
                }
                else
                {
                    // Empilha tudo
                    destinationItem.IncreaseAmount(originItem.Amount);
                    originItem.SetAmount(); // Zera origem
                }
            }
            else
            {
                // Troca de posições ou move para slot vazio
                if (destinationItem.ItemId == 0)
                {
                    // Move para slot vazio
                    var tempItem = (ItemModel)originItem.Clone(destinationItem.Id);
                    tempItem.Slot = destinationItem.Slot;

                    destinationItem = tempItem;
                    originItem.SetItemId(); // Zera origem
                }
                else
                {
                    // Troca posições
                    var tempItem = (ItemModel)destinationItem.Clone(originItem.Id);
                    tempItem.Slot = originItem.Slot;

                    var tempItem2 = (ItemModel)originItem.Clone(destinationItem.Id);
                    tempItem2.Slot = destinationItem.Slot;

                    destinationItem = tempItem2;
                    originItem = (ItemModel)tempItem.Clone(originItem.Id);
                }
            }

            // Atualiza os slots na lista
            Items[originSlot] = originItem;
            Items[destinationSlot] = destinationItem;

            return true;
        }

        /// <summary>
        /// Limpa todos os itens do inventário zerando suas propriedades.
        /// </summary>
        public void Clear()
        {
            foreach (var item in Items)
            {
                item.SetItemId();
                item.SetAmount();
                item.SetRemainingTime();
                item.SetSellPrice(0);
            }
        }

        /// <summary>
        /// Remove ou reduz múltiplos itens do inventário.
        /// </summary>
        /// <param name="itemsToRemoveOrReduce">Lista de itens a remover/reduzir</param>
        /// <returns>True se todos os itens foram processados com sucesso</returns>
        public bool RemoveOrReduceItems(List<ItemModel> itemsToRemoveOrReduce)
        {
            var backup = BackupOperation();

            foreach (var itemToRemove in itemsToRemoveOrReduce)
            {
                if (itemToRemove.Amount == 0 || itemToRemove.ItemId == 0)
                    continue;

                var targetItems = FindItemsById(itemToRemove.ItemId);

                foreach (var targetItem in targetItems)
                {
                    if (targetItem.Amount >= itemToRemove.Amount)
                    {
                        // Remove quantidade completa de um slot
                        targetItem.ReduceAmount(itemToRemove.Amount);
                        itemToRemove.SetAmount(); // Marca como processado
                        break;
                    }
                    else
                    {
                        // Remove tudo deste slot e continua
                        itemToRemove.ReduceAmount(targetItem.Amount);
                        targetItem.SetAmount(); // Zera slot
                    }
                }

                // Se ainda resta quantidade a remover, falha
                if (itemToRemove.Amount > 0)
                {
                    RevertOperation(backup);
                    return false;
                }
            }

            CheckEmptyItems();
            return true;
        }

        /// <summary>
        /// Remove ou reduz múltiplos itens com opção de reorganizar slots.
        /// </summary>
        /// <param name="itemsToRemoveOrReduce">Lista de itens a remover/reduzir</param>
        /// <param name="reArrangeSlots">Se deve reorganizar slots após operação</param>
        /// <returns>True se todos os itens foram processados com sucesso</returns>
        public bool RemoveOrReduceItems(List<ItemModel> itemsToRemoveOrReduce, bool reArrangeSlots = true)
        {
            var backup = BackupOperation();

            foreach (var itemToRemove in itemsToRemoveOrReduce)
            {
                if (itemToRemove.Amount == 0 || itemToRemove.ItemId == 0)
                    continue;

                var targetItems = FindItemsById(itemToRemove.ItemId);

                foreach (var targetItem in targetItems)
                {
                    if (targetItem.Amount >= itemToRemove.Amount)
                    {
                        targetItem.ReduceAmount(itemToRemove.Amount);
                        itemToRemove.SetAmount(); // Marca como processado
                        break;
                    }

                    itemToRemove.ReduceAmount(targetItem.Amount);
                    targetItem.SetAmount(); // Zera slot
                }

                if (itemToRemove.Amount <= 0)
                {
                    continue;
                }

                // Falha se não conseguiu remover tudo
                RevertOperation(backup);
                return false;
            }

            CheckEmptyItemsThenRearrangeSlots();
            return true;
        }

        /// <summary>
        /// Remove ou reduz item específico com quantidade e slot opcionais.
        /// </summary>
        /// <param name="itemToRemove">Item a ser removido</param>
        /// <param name="amount">Quantidade a remover</param>
        /// <param name="slot">Slot específico (opcional, -1 para qualquer slot)</param>
        /// <returns>True se removido com sucesso</returns>
        public bool RemoveOrReduceItem(ItemModel? itemToRemove, int amount, int slot = -1)
        {
            if (itemToRemove == null || amount == 0) return false;

            var tempItem = (ItemModel?)itemToRemove.Clone();
            tempItem?.SetAmount(amount);

            return slot > -1 ? RemoveOrReduceItemWithSlot(tempItem, slot) : RemoveOrReduceItemWithoutSlot(tempItem);
        }

        /// <summary>
        /// Adiciona múltiplos slots ao inventário.
        /// </summary>
        /// <param name="amount">Quantidade de slots a adicionar</param>
        /// <returns>Lista dos novos slots criados</returns>
        public List<ItemModel> AddSlotsAll(byte amount = 1)
        {
            List<ItemModel> newSlots = new List<ItemModel>();
            for (byte i = 0; i < amount; i++)
            {
                var newItemSlot = new ItemModel(Items.Max(x => x.Slot))
                {
                    ItemListId = Id
                };

                newSlots.Add(newItemSlot);
                Items.Add(newItemSlot);
                Size++;
            }

            return newSlots;
        }

        /// <summary>
        /// Remove ou reduz item de um slot específico.
        /// </summary>
        /// <param name="itemToRemove">Item a ser removido</param>
        /// <param name="slot">Slot específico</param>
        /// <returns>True se removido com sucesso</returns>
        public bool RemoveOrReduceItemWithSlot(ItemModel? itemToRemove, int slot)
        {
            if (itemToRemove == null || itemToRemove.Amount == 0 || itemToRemove.ItemId == 0)
                return false;

            var backup = BackupOperation();

            var targetItem = FindItemBySlot(slot);
            targetItem?.ReduceAmount(itemToRemove.Amount);
            itemToRemove.SetAmount(); // Marca como processado

            if (itemToRemove.Amount > 0)
            {
                RevertOperation(backup);
                return false;
            }

            CheckEmptyItems();
            return true;
        }

        /// <summary>
        /// Remove ou reduz item sem especificar slot (busca automaticamente).
        /// </summary>
        /// <param name="itemToRemove">Item a ser removido</param>
        /// <returns>True se removido com sucesso</returns>
        public bool RemoveOrReduceItemWithoutSlot(ItemModel? itemToRemove)
        {
            if (itemToRemove == null || itemToRemove.Amount == 0 || itemToRemove.ItemId == 0)
                return false;

            var backup = BackupOperation();

            var targetItems = FindItemsById(itemToRemove.ItemId);

            foreach (var targetItem in targetItems)
            {
                if (targetItem.Amount >= itemToRemove.Amount)
                {
                    targetItem.ReduceAmount(itemToRemove.Amount);
                    itemToRemove.SetAmount(); // Marca como processado
                    break;
                }
                else
                {
                    itemToRemove.ReduceAmount(targetItem.Amount);
                    targetItem.SetAmount(); // Zera slot
                }
            }

            if (itemToRemove.Amount > 0)
            {
                RevertOperation(backup);
                return false;
            }

            CheckEmptyItems();
            return true;
        }

        /// <summary>
        /// Remove item específico de um slot específico.
        /// </summary>
        /// <param name="itemToRemove">Item a ser removido</param>
        /// <param name="slot">Slot específico</param>
        /// <returns>True se removido com sucesso</returns>
        public bool RemoveItem(ItemModel itemToRemove, short slot)
        {
            if (itemToRemove == null || itemToRemove.Amount == 0 || itemToRemove.ItemId == 0)
                return false;

            var backup = BackupOperation();

            var targetItem = FindItemBySlot(slot);

            if (targetItem == null)
                return false;

            if (targetItem.Amount >= itemToRemove.Amount)
            {
                targetItem.ReduceAmount(itemToRemove.Amount);
                itemToRemove.SetAmount(); // Marca como processado
                CheckEmptyItems();
                return true;
            }
            else
            {
                RevertOperation(backup);
                CheckEmptyItems();
                return false;
            }
        }

        /// <summary>
        /// Verifica e limpa itens vazios ou inválidos.
        /// Zera propriedades de itens com ItemId = 0 ou Amount <= 0.
        /// </summary>
        public void CheckEmptyItems()
        {
            Items.ForEach(item =>
            {
                if (item.ItemId == 0 || item.Amount <= 0)
                {
                    item.SetItemId();
                    item.SetAmount();
                    item.SetRemainingTime();
                    item.SetSellPrice(0);
                }
            });
        }

        /// <summary>
        /// Verifica itens vazios e reorganiza slots sequencialmente.
        /// Itens válidos ficam no início, slots vazios no final.
        /// </summary>
        public void CheckEmptyItemsThenRearrangeSlots()
        {
            // Limpa itens inválidos
            foreach (var item in Items.Where(item => item.ItemId == 0 || item.Amount <= 0))
            {
                item.SetItemId();
                item.SetAmount();
                item.SetRemainingTime();
                item.SetSellPrice(0);
            }

            // Reorganiza slots: itens válidos primeiro, vazios depois
            int slot = 0;
            Items = Items
                .OrderBy(item => item.ItemId > 0 ? 0 : 1) // Itens válidos (ItemId > 0) primeiro
                .ThenBy(item => item.Slot) // Mantém ordem original dos slots
                .Select(item =>
                {
                    item.SetSlot(slot++); // Reassigna slots sequencialmente
                    return item;
                })
                .ToList();
        }

        /// <summary>
        /// Serializa a instância atual em array de bytes.
        /// Cada item ocupa exatamente 59 bytes conforme GeneralSizeEnum.ItemSizeInBytes.
        /// </summary>
        /// <returns>Array de bytes resultante</returns>
        public byte[] ToArray()
        {
            byte[] buffer;

            using (MemoryStream m = new())
            {
                var sortedItems = Items.OrderBy(x => x.Slot);

                foreach (var item in sortedItems)
                    m.Write(item.ToArray(), 0, GeneralSizeEnum.ItemSizeInBytes.GetHashCode());

                buffer = m.ToArray();
            }

            return buffer;
        }

        /// <summary>
        /// Serializa apenas itens válidos para sistema de presentes.
        /// Filtra itens com ItemId > 0 e serializa cada um em 59 bytes.
        /// </summary>
        /// <returns>Array de bytes dos itens válidos ou array vazio</returns>
        public byte[] GiftToArray()
        {
            byte[] buffer;

            using MemoryStream m = new();
            var filteredItems = Items.Where(x => x.ItemId > 0).OrderBy(x => x.Slot);
            var filteredItemsList = filteredItems.ToList();

            if (filteredItemsList.Any())
            {
                foreach (var item in filteredItemsList)
                {
                    m.Write(item.GiftToArray(), 0, GeneralSizeEnum.ItemSizeInBytes.GetHashCode());
                }

                buffer = m.ToArray();
            }
            else
            {
                // Retorna 59 bytes vazios se não há itens válidos
                byte[] emptyBuffer = new byte[GeneralSizeEnum.ItemSizeInBytes.GetHashCode()];
                return emptyBuffer;
            }

            return buffer;
        }

        /// <summary>
        /// Nova versão da serialização de presentes usando timestamp Unix.
        /// Filtra itens válidos e usa NewGiftToArray() para cada item.
        /// </summary>
        /// <returns>Array de bytes dos itens válidos ou array vazio</returns>
        public byte[] NewGiftToArray()
        {
            byte[] buffer;

            using MemoryStream m = new();
            var filteredItems = Items.Where(x => x.ItemId > 0).OrderBy(x => x.Slot);
            var filteredItemsList = filteredItems.ToList();

            if (filteredItemsList.Any())
            {
                foreach (var item in filteredItemsList)
                {
                    var itemArray = item.NewGiftToArray();
                    m.Write(itemArray, 0, itemArray.Length);
                }

                buffer = m.ToArray();
            }
            else
            {
                // Retorna 59 bytes vazios se não há itens válidos
                byte[] emptyBuffer = new byte[GeneralSizeEnum.ItemSizeInBytes.GetHashCode()];
                return emptyBuffer;
            }

            return buffer;
        }

        /// <summary>
        /// Converte a instância atual em representação string para debug.
        /// Lista todos os itens com seus slots e propriedades.
        /// </summary>
        /// <returns>String formatada com informações do inventário</returns>
        public string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Inventory{Id}");
            foreach (var item in Items.OrderBy(x => x.Slot))
            {
                sb.AppendLine($"Item[{item.Slot}] - {item.ItemId}");
                sb.AppendLine(item.ToString());
            }

            return sb.ToString();
        }
    }
}