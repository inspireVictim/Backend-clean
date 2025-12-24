using AutoMapper;
using YessBackend.Application.DTOs.Auth;
using YessBackend.Application.DTOs.Wallet;
using YessBackend.Application.DTOs.Partner;
using YessBackend.Application.DTOs.Order;
using YessBackend.Domain.Entities;

namespace YessBackend.Application.Mappings;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        // User mappings - сохраняем твой явный стиль
        CreateMap<User, UserResponseDto>()
            .ForMember(dest => dest.Phone, opt => opt.MapFrom(src => src.Phone))
            .ForMember(dest => dest.Email, opt => opt.MapFrom(src => src.Email))
            .ForMember(dest => dest.FirstName, opt => opt.MapFrom(src => src.FirstName))
            .ForMember(dest => dest.LastName, opt => opt.MapFrom(src => src.LastName))
            .ForMember(dest => dest.AvatarUrl, opt => opt.MapFrom(src => src.AvatarUrl))
            .ForMember(dest => dest.PhoneVerified, opt => opt.MapFrom(src => src.PhoneVerified))
            .ForMember(dest => dest.EmailVerified, opt => opt.MapFrom(src => src.EmailVerified))
            .ForMember(dest => dest.CityId, opt => opt.MapFrom(src => src.CityId))
            .ForMember(dest => dest.ReferralCode, opt => opt.MapFrom(src => src.ReferralCode))
            .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => src.CreatedAt))
            // Добавляем игнорирование для нового поля, его заполним в сервисе
            .ForMember(dest => dest.ReferralsCount, opt => opt.Ignore());

        // Wallet mappings
        CreateMap<Wallet, WalletResponseDto>();

        // Transaction mappings
        CreateMap<Transaction, TransactionResponseDto>();

        // Partner mappings
        CreateMap<Partner, PartnerResponseDto>();
        CreateMap<PartnerLocation, PartnerLocationResponseDto>();

        // Order mappings - сохраняем твою логику
        CreateMap<Order, OrderResponseDto>()
            .ForMember(dest => dest.TransactionNumber, opt =>
                opt.MapFrom(src => src.Transaction != null ? src.Transaction.TransactionNumber : null));

        CreateMap<OrderItem, OrderItemResponseDto>();
    }
}