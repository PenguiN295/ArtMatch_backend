using AutoMapper;
using ArtMatch.Entities;
public class MapperProfile : Profile
{
    public MapperProfile()
    {
        CreateMap<Photo, PhotoDTO>()
            .ForMember(dest => dest.Photo_data, opt => opt.MapFrom(src => src.Photo_data))
            .ForMember(dest => dest.UserId, opt => opt.MapFrom(src => src.UserId))
            .ForMember(dest => dest.UploadedAt, opt => opt.MapFrom(src => src.UploadedAt));
        CreateMap<PhotoDTO, Photo>();
    }
}