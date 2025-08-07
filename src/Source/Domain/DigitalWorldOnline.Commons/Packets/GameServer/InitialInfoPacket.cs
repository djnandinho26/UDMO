using DigitalWorldOnline.Commons.Models.Character;
using DigitalWorldOnline.Commons.Models.Map;
using DigitalWorldOnline.Commons.Models.Mechanics;
using DigitalWorldOnline.Commons.Utils;
using DigitalWorldOnline.Commons.Writers;
using System.Collections.Generic;

namespace DigitalWorldOnline.Commons.Packets.GameServer
{
    public class InitialInfoPacket : PacketWriter
    {
        private const int PacketNumber = 1003;

        public static byte[] ToByteArray(string hexString)
        {
            int NumberChars = hexString.Length;
            byte[] bytes = new byte[NumberChars / 2];

            for (int i = 0; i < NumberChars; i += 2)
                bytes[i / 2] = Convert.ToByte(hexString.Substring(i, 2), 16);

            return bytes;
        }
        /// <summary>
        /// Construtor que cria o pacote de informações iniciais para o spawn do personagem no jogo.
        /// Este pacote contém todas as informações necessárias para inicializar o personagem no cliente,
        /// incluindo dados do tamer, do partner, equipamentos, buffs, grupo (party) e configurações gerais.
        /// </summary>
        /// <param name="character">Modelo do personagem (tamer) que está tentando fazer login no jogo</param>
        /// <param name="party">Grupo (party) do qual o personagem faz parte, ou null se não estiver em grupo</param>
        public InitialInfoPacket(CharacterModel character, GameParty? party)
        {
            Type(PacketNumber); // Define o tipo do pacote (1003)

            // === INFORMAÇÕES BÁSICAS DO PERSONAGEM ===
            WriteInt(1); // Flag de inicialização
            WriteInt(character.Location.X); // Posição X no mapa
            WriteInt(character.Location.Y); // Posição Y no mapa
            WriteInt(character.GeneralHandler); // Handler único do personagem
            WriteInt(character.Model.GetHashCode()); // Modelo visual do personagem
            WriteString(character.Name); // Nome do personagem

            // === STATUS E ATRIBUTOS DO TAMER ===
            WriteInt64(character.CurrentExperience * 100); // Experiência atual (multiplicada por 100)
            WriteShort(character.Level); // Nível atual
            WriteInt(character.HP); // HP máximo
            WriteInt(character.DS); // DS máximo (Digi Soul)
            WriteInt(character.CurrentHp); // HP atual
            WriteInt(character.CurrentDs); // DS atual
            WriteInt(CharacterModel.Fatigue); // Nível de fadiga (sempre 0)
            WriteInt(character.AT); // Poder de ataque
            WriteInt(character.DE); // Defesa
            WriteInt(character.MS); // Velocidade de movimento

            // === EQUIPAMENTOS E INVENTÁRIOS ===
            WriteBytes(character.Equipment.ToArray()); // Equipamentos equipados
            WriteBytes(character.ChipSets.ToArray()); // Chipsets do digivice
            WriteBytes(character.Digivice.ToArray()); // Digivice
            WriteBytes(character.TamerSkill.ToArray()); // Habilidades do tamer
            WriteBytes(character.Progress.ToArray()); // Progresso/conquistas

            // === INCUBADORA ===
            WriteInt(character.Incubator.EggId); // ID do ovo na incubadora
            WriteInt(character.Incubator.HatchLevel); // Nível de choque do ovo
            WriteInt(-1); // Tempo limite de troca do ovo
            WriteInt(character.Incubator.BackupDiskId); // ID do disco de backup
            WriteInt(-1); // Tempo limite de troca do disco

            // === BUFFS DO TAMER ===
            WriteShort((short)character.BuffList.ActiveBuffs.Count); // Quantidade de buffs ativos
            foreach (var buff in character.BuffList.ActiveBuffs.ToList())
            {
                WriteShort((short)buff.BuffId); // ID do buff
                WriteShort((short)buff.TypeN); // Tipo/nível do buff
                WriteInt(UtilitiesFunctions.RemainingTimeSeconds(buff.RemainingSeconds)); // Tempo restante
                WriteInt(buff.SkillId); // ID da habilidade relacionada
            }

            // === INFORMAÇÕES DO PARTNER (DIGIMON PRINCIPAL) ===
            WriteByte(character.DigimonSlots); // Quantidade de slots de digimon
            WriteInt(character.Partner.GeneralHandler); // Handler do partner
            WriteInt(character.Partner.CurrentType); // Tipo/forma atual
            WriteString(character.Partner.Name); // Nome do partner
            WriteByte((byte)character.Partner.HatchGrade); // Grau de choque (3-5 estrelas)
            WriteShort(character.Partner.Size); // Tamanho do digimon

            // === STATUS DO PARTNER ===
            WriteInt64(character.Partner.CurrentExperience * 100); // Experiência atual
            WriteInt64(character.Partner.TranscendenceExperience); // Experiência de transcendência
            WriteShort(character.Partner.Level); // Nível
            WriteInt(character.Partner.HP); // HP máximo
            WriteInt(character.Partner.DS); // DS máximo
            WriteInt(character.Partner.DE); // Defesa
            WriteInt(character.Partner.AT); // Ataque
            WriteInt(character.Partner.CurrentHp); // HP atual
            WriteInt(character.Partner.CurrentDs); // DS atual
            WriteInt(character.Partner.FS); // Amizade (Friendship)
            WriteInt(0); // Campo reservado
            WriteInt(character.Partner.EV); // Evasão
            WriteInt(character.Partner.CC); // Chance crítica
            WriteInt(character.Partner.MS); // Velocidade de movimento
            WriteInt(character.Partner.AS); // Velocidade de ataque
            WriteInt(0); // Campo reservado
            WriteInt(character.Partner.HT); // Taxa de acerto
            WriteInt(0); // Campo reservado
            WriteInt(0); // Campo reservado
            WriteInt(character.Partner.AR); // Alcance de ataque
            WriteInt(character.Partner.BL); // Taxa de bloqueio
            WriteInt(character.Partner.BaseType); // Tipo base do digimon

            // === EVOLUÇÕES DO PARTNER ===
            WriteByte((byte)character.Partner.Evolutions.Count); // Quantidade de evoluções desbloqueadas
            for (int i = 0; i < character.Partner.Evolutions.Count; i++)
            {
                var form = character.Partner.Evolutions[i];
                WriteBytes(form.ToArray()); // Dados da evolução
            }

            // === DIGICLONE DO PARTNER ===
            WriteShort(character.Partner.Digiclone.CloneLevel); // Nível do clone
            WriteShort(character.Partner.Digiclone.ATValue); // Valor AT do clone
            WriteShort(character.Partner.Digiclone.BLValue); // Valor BL do clone
            WriteShort(character.Partner.Digiclone.CTValue); // Valor CT do clone
            WriteShort(0); // DE Value (não implementado no cliente)
            WriteShort(character.Partner.Digiclone.EVValue); // Valor EV do clone
            WriteShort(0); // HT Value (não implementado no cliente)
            WriteShort(character.Partner.Digiclone.HPValue); // Valor HP do clone

            // Níveis de upgrade do digiclone
            WriteShort(character.Partner.Digiclone.ATLevel); // Nível AT do clone
            WriteShort(character.Partner.Digiclone.BLLevel); // Nível BL do clone
            WriteShort(character.Partner.Digiclone.CTLevel); // Nível CT do clone
            WriteShort(0); // DE Level (não implementado no cliente)
            WriteShort(character.Partner.Digiclone.EVLevel); // Nível EV do clone
            WriteShort(0); // HT Level (não implementado no cliente)
            WriteShort(character.Partner.Digiclone.HPLevel); // Nível HP do clone

            // === BUFFS DO PARTNER ===
            WriteShort((short)character.Partner.BuffList.ActiveBuffs.Count); // Quantidade de buffs
            foreach (var buff in character.Partner.BuffList.ActiveBuffs)
            {
                WriteShort((short)buff.BuffId); // ID do buff
                WriteShort((short)buff.TypeN); // Tipo do buff
                WriteInt(UtilitiesFunctions.RemainingTimeSeconds(buff.RemainingSeconds)); // Tempo restante
                WriteInt(buff.SkillId); // ID da habilidade
            }

            // === EXPERIÊNCIA DE ATRIBUTOS DO PARTNER ===
            // Atributos de tipo
            WriteShort(character.Partner.AttributeExperience.Data); // Exp. Data
            WriteShort(character.Partner.AttributeExperience.Vaccine); // Exp. Vaccine
            WriteShort(character.Partner.AttributeExperience.Virus); // Exp. Virus

            // Atributos elementais
            WriteShort(character.Partner.AttributeExperience.Ice); // Exp. Gelo
            WriteShort(character.Partner.AttributeExperience.Water); // Exp. Água
            WriteShort(character.Partner.AttributeExperience.Fire); // Exp. Fogo
            WriteShort(character.Partner.AttributeExperience.Land); // Exp. Terra
            WriteShort(character.Partner.AttributeExperience.Wind); // Exp. Vento
            WriteShort(character.Partner.AttributeExperience.Wood); // Exp. Madeira
            WriteShort(character.Partner.AttributeExperience.Light); // Exp. Luz
            WriteShort(character.Partner.AttributeExperience.Dark); // Exp. Trevas
            WriteShort(character.Partner.AttributeExperience.Thunder); // Exp. Trovão
            WriteShort(character.Partner.AttributeExperience.Steel); // Exp. Aço

            WriteInt(0); // nUID (não é mais utilizado)
            WriteByte(0); // Quantidade de habilidades cash (se > 0, deve informar os objetos)

            // === DIGIMONS ADICIONAIS (MERCENÁRIOS) ===
            byte slot = 1; // Começa do slot 1 (slot 0 é o partner)
            foreach (var digimon in character.ActiveDigimons)
            {
                WriteByte(slot); // Número do slot
                WriteUInt(digimon.GeneralHandler); // Handler do digimon
                WriteInt(digimon.BaseType); // Tipo base
                WriteString(digimon.Name); // Nome
                WriteByte((byte)digimon.HatchGrade); // Grau de choque
                WriteShort(digimon.Size); // Tamanho

                // Status do digimon mercenário (similar ao partner)
                WriteInt64(digimon.CurrentExperience * 100);
                WriteInt64(digimon.TranscendenceExperience);
                WriteShort(digimon.Level);
                WriteInt(digimon.HP);
                WriteInt(digimon.DS);
                WriteInt(digimon.DE);
                WriteInt(digimon.AT);
                WriteInt(digimon.CurrentHp);
                WriteInt(digimon.CurrentDs);
                WriteInt(digimon.FS);
                WriteInt(0); // Campo reservado
                WriteInt(digimon.EV);
                WriteInt(digimon.CC);
                WriteInt(digimon.MS);
                WriteInt(digimon.AS);
                WriteInt(0); // Campo reservado
                WriteInt(digimon.HT);
                WriteInt(0); // Campo reservado
                WriteInt(0); // Campo reservado
                WriteInt(0); // Campo reservado
                WriteInt(digimon.BL);
                WriteInt(digimon.BaseType);

                // Evoluções do mercenário
                WriteByte((byte)digimon.Evolutions.Count);
                for (int i = 0; i < digimon.Evolutions.Count; i++)
                {
                    var form = digimon.Evolutions[i];
                    WriteBytes(form.ToArray());
                }

                // Digiclone do mercenário
                WriteShort(digimon.Digiclone.CloneLevel);
                WriteShort(digimon.Digiclone.ATValue);
                WriteShort(digimon.Digiclone.BLValue);
                WriteShort(digimon.Digiclone.CTValue);
                WriteShort(0); // DE Value (não implementado)
                WriteShort(digimon.Digiclone.EVValue);
                WriteShort(0); // HT Value (não implementado)
                WriteShort(digimon.Digiclone.HPValue);
                WriteShort(digimon.Digiclone.ATLevel);
                WriteShort(digimon.Digiclone.BLLevel);
                WriteShort(digimon.Digiclone.CTLevel);
                WriteShort(0); // DE Level (não implementado)
                WriteShort(digimon.Digiclone.EVLevel);
                WriteShort(0); // HT Level (não implementado)
                WriteShort(digimon.Digiclone.HPLevel);

                // Experiência de atributos do mercenário
                WriteShort(digimon.AttributeExperience.Data);
                WriteShort(digimon.AttributeExperience.Vaccine);
                WriteShort(digimon.AttributeExperience.Virus);
                WriteShort(digimon.AttributeExperience.Ice);
                WriteShort(digimon.AttributeExperience.Water);
                WriteShort(digimon.AttributeExperience.Fire);
                WriteShort(digimon.AttributeExperience.Land);
                WriteShort(digimon.AttributeExperience.Wind);
                WriteShort(digimon.AttributeExperience.Wood);
                WriteShort(digimon.AttributeExperience.Light);
                WriteShort(digimon.AttributeExperience.Dark);
                WriteShort(digimon.AttributeExperience.Thunder);
                WriteShort(digimon.AttributeExperience.Steel);

                WriteInt(16404); // Valor fixo
                WriteByte(0); // Campo reservado

                slot++; // Próximo slot
            }

            WriteByte(99); // Marca o fim do loop de digimons
            WriteInt(0); // Campo reservado
            WriteInt(character.Channel); // Canal atual do personagem
            WriteBytes(character.SerializeMapRegion()); // Regiões do mapa descobertas
            WriteInt(character.DigimonArchive.Slots); // Slots do arquivo de digimons

            // === INFORMAÇÕES DO GRUPO (PARTY) ===
            if (party != null) // Se o personagem está em um grupo
            {
                WriteUInt((uint)party.Id); // ID do grupo
                WriteInt((int)party.LootType); // Tipo de distribuição de loot
                WriteByte((byte)party.LootFilter); // Filtro de raridade
                WriteByte(0); // Grau de raridade
                WriteByte((byte)(party.LeaderSlot)); // Slot do líder do grupo

                // Informações dos membros do grupo (exceto o próprio personagem)
                foreach (var member in party.Members.Where(x => x.Value.Id != character.Id))
                {
                    WriteByte(member.Key); // Slot do membro no grupo

                    // Verifica se o membro está no mesmo canal e mapa
                    if (character.Channel == member.Value.Channel &&
                        character.Location.MapId == member.Value.Location.MapId)
                    {
                        WriteInt(member.Value.GeneralHandler); // Handler do membro
                        WriteInt(member.Value.Partner.GeneralHandler); // Handler do partner do membro
                    }
                    else
                    {
                        WriteInt(0); // Membro não visível
                        WriteInt(0); // Partner não visível
                    }

                    // Informações básicas do membro
                    WriteInt(member.Value.Model.GetHashCode()); // Modelo do membro
                    WriteShort(member.Value.Level); // Nível do membro
                    WriteString(member.Value.Name); // Nome do membro

                    // Informações do partner do membro
                    WriteInt(member.Value.Partner.CurrentType); // Tipo do partner
                    WriteShort(member.Value.Partner.Level); // Nível do partner
                    WriteString(member.Value.Partner.Name); // Nome do partner

                    WriteInt(member.Value.Location.MapId); // Mapa do membro
                    WriteInt(member.Value.Channel); // Canal do membro
                }

                WriteByte(99); // Fim do loop de membros do grupo
            }
            else // Personagem não está em grupo
            {
                WriteInt(0); // ID do grupo (0 = sem grupo)
                WriteInt(0); // Tipo de loot
                WriteByte(0); // Filtro de raridade
                WriteByte(0); // Grau de raridade
                WriteByte(0); // Slot do líder

                WriteByte(99); // Fim do loop (sem membros)
            }

            // === INFORMAÇÕES FINAIS ===
            WriteShort(character.CurrentTitle); // Título atual do personagem

            // Cooldowns de itens (máximo 32 slots)
            for (int i = 0; i < 32; i++)
                WriteInt(0); // Todos os cooldowns zerados

            // Informações diversas
            WriteInt(0); // Versão do jogo
            WriteInt(2); // Histórico de dias de trabalho (eventos de login diário)
            WriteInt(0); // Tempo de presença hoje
            WriteInt(0); // ID do boss vivo no mapa atual
            WriteByte(0); // PC Bang status

            // === LOJA CONSIGNADA ===
            if (character.ConsignedShop != null) // Se o personagem tem loja consignada
            {
                WriteInt(character.ConsignedShop.Location.MapId); // Mapa da loja
                WriteInt(character.ConsignedShop.Channel); // Canal da loja
                WriteInt(character.ConsignedShop.Location.X); // Posição X da loja
                WriteInt(character.ConsignedShop.Location.Y); // Posição Y da loja
                WriteInt(character.ConsignedShop.ItemId); // ID do item da loja
            }
            else
                WriteInt(0); // Sem loja consignada

            // Configurações adicionais
            WriteInt(0); // Opções do cliente (relacionado ao tutorial)
            WriteInt(0); // Rank de conquistas

            WriteByte(0); // Se o minigame de choque já foi executado
            WriteShort(0); // Total de sucessos no minigame

            // === HABILIDADES DO TAMER ===
            // Habilidades normais com cooldown ativo
            var Buffs = character.ActiveSkill.Where(x =>
                x.Type == Enums.ClientEnums.TamerSkillTypeEnum.Normal && x.SkillId > 0 &&
                x.RemainingCooldownSeconds > 0).ToList();

            if (Buffs.Any())
            {
                WriteByte((byte)Buffs.Count); // Quantidade de habilidades
                foreach (var buff in Buffs)
                {
                    WriteInt(buff.SkillId); // ID da habilidade
                    if (buff.RemainingCooldownSeconds > 0)
                    {
                        WriteInt(UtilitiesFunctions.RemainingTimeSeconds(buff.RemainingCooldownSeconds)); // Cooldown restante
                    }
                    else
                    {
                        WriteInt(0); // Sem cooldown
                    }
                }
            }
            else
            {
                WriteByte(0); // Nenhuma habilidade ativa
            }

            // Habilidades cash ativas
            var cashBuffs = character.ActiveSkill.Where(x =>
                    x.Type == Enums.ClientEnums.TamerSkillTypeEnum.Cash && x.SkillId > 0 && x.RemainingMinutes > 0)
                .ToList();

            if (cashBuffs.Any())
            {
                WriteByte((byte)cashBuffs.Count); // Quantidade de habilidades cash
                foreach (var buff in cashBuffs)
                {
                    if (buff.RemainingMinutes > 0)
                    {
                        WriteInt(buff.SkillId); // ID da habilidade
                        WriteInt(UtilitiesFunctions.RemainingTimeMinutes(buff.RemainingMinutes)); // Duração restante
                        if (buff.RemainingCooldownSeconds > 0)
                        {
                            WriteInt(UtilitiesFunctions.RemainingTimeSeconds(buff.RemainingCooldownSeconds)); // Cooldown
                        }
                        else
                        {
                            WriteInt(0); // Sem cooldown
                        }
                    }
                    else
                    {
                        WriteInt(0);
                        WriteInt(0);
                        WriteInt(0);
                    }
                }
            }
            else
            {
                WriteByte(0); // Nenhuma habilidade cash ativa
            }

            // === CONFIGURAÇÕES FINAIS ===
            WriteByte(0); // Bloqueio de chat (se 1, deve informar duração)
            WriteByte(0); // Master match (1 = equipe A, 2 = equipe B)

            // Deck buff da enciclopédia
            if (character.DeckBuffId == null)
            {
                WriteByte(0); // Sem deck buff
            }
            else
            {
                WriteInt((int)character.DeckBuffId); // ID do deck buff ativo
            }

            WriteByte(0); // Ban de megafone (1 = bloqueado)
            WriteInt(0); // Campo reservado
            WriteBytes(new byte[29]); // Bytes finais de preenchimento
        }

    }
}