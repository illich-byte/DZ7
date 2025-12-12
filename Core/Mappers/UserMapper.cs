// Core/Mappers/UserMapper.cs
// ...
using AutoMapper;
using Core.Models.Account;
using Domain.Entities.Idenity;

namespace Core.Mappers;

public class UserMapper : Profile
{
    public UserMapper()
    {
        CreateMap<UserEntity, UserProfileModel>();
    }
}