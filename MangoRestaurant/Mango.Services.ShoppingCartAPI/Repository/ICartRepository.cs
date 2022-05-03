using Mango.Services.ShoppingCartAPI.Models.Dto;

namespace Mango.Services.ShoppingCartAPI.Repository
{
    public interface ICartRepository
    {
        Task<bool> ClearCart(string userId);
        Task<CartDto> CreateUpdateCart(CartDto cartDto);
        Task<CartDto> GetCartByUserId(string Userid);
        Task<bool> ApplyCoupon(string userId, string couponCode);
        Task<bool> RemoveCoupon(string userId);
        Task<bool> RemoveFromCart(int cartDetailsId);
    }
}