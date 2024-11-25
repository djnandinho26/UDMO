using AutoMapper;
using DigitalWorldOnline.Commons.DTOs.Assets;
using DigitalWorldOnline.Commons.DTOs.Config;
using DigitalWorldOnline.Commons.DTOs.Config.Events;
using DigitalWorldOnline.Commons.Models.Config.Events;
using DigitalWorldOnline.Commons.Models.Summon;
using DigitalWorldOnline.Commons.ViewModel.Asset;
using DigitalWorldOnline.Commons.ViewModel.Mobs;
using DigitalWorldOnline.Commons.ViewModel.Summons;

namespace DigitalWorldOnline.Admin.Data
{
    public class AdminMappingProfile : Profile
    {
        public AdminMappingProfile()
        {
            CreateMap<MonsterBaseInfoAssetDTO, MobAssetViewModel>();
            CreateMap<MobAssetViewModel, MobCreationViewModel>();
            CreateMap<MobAssetViewModel, MobUpdateViewModel>();
            CreateMap<MobCreationViewModel, MobConfigDTO>();
            CreateMap<MobLocationViewModel, MobLocationConfigDTO>();
            CreateMap<MobExpRewardViewModel, MobExpRewardConfigDTO>();
            CreateMap<MobDropRewardViewModel, MobDropRewardConfigDTO>();
            CreateMap<MobItemDropViewModel, ItemDropConfigDTO>();
            CreateMap<MobBitDropViewModel, BitsDropConfigDTO>();
            CreateMap<SummonViewModel, SummonDTO>()
                .ReverseMap();
            CreateMap<MobAssetViewModel, SummonMobViewModel>()
                .ForMember(dest => dest.ExpReward, opt => opt.Ignore())
                .ForMember(dest => dest.Location, opt => opt.Ignore())
                .ForMember(dest => dest.DropReward, opt => opt.Ignore())
                .ForMember(dest => dest.Duration, opt => opt.Ignore());
            CreateMap<ItemAssetDTO, ItemAssetViewModel>();
            CreateMap<MobConfigDTO, MobUpdateViewModel>()
                .ReverseMap();
            
            CreateMap<EventConfigModel, EventConfigDTO>()
                .ReverseMap();

            CreateMap<EventMapsConfigModel, EventMapsConfigDTO>()
                .ReverseMap();

            CreateMap<EventBitsDropConfigModel, EventBitsDropConfigDTO>()
                .ReverseMap();

            CreateMap<EventItemDropConfigModel, EventItemDropConfigDTO>()
                .ReverseMap();

            CreateMap<EventMobConfigModel, EventMobConfigDTO>()
                .ReverseMap();

            CreateMap<EventMobDropRewardConfigModel, EventMobDropRewardConfigDTO>()
                .ReverseMap();

            CreateMap<EventMobExpRewardConfigModel, EventMobExpRewardConfigDTO>()
                .ReverseMap();

            CreateMap<EventMobLocationConfigModel, EventMobLocationConfigDTO>()
                .ReverseMap();
        }
    }
}
