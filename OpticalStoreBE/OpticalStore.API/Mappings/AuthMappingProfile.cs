using AutoMapper;
using OpticalStore.API.Requests.Auth;
using OpticalStore.API.Responses.Auth;
using OpticalStore.BLL.DTOs;

namespace OpticalStore.API.Mappings
{
    public class AuthMappingProfile : Profile
    {
        public AuthMappingProfile()
        {
            CreateMap<RegisterRequest, RegisterRequestDto>();
            CreateMap<LoginRequest, LoginRequestDto>();
            CreateMap<UserDto, UserResponse>();
            CreateMap<AuthResultDto, AuthResponse>();
        }
    }
}
