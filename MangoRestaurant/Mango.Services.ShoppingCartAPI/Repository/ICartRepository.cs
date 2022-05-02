using Mango.Services.ShoppingCartAPI.Models.Dto;

namespace Mango.Services.ShoppingCartAPI.Repository
{
    public interface ICartRepository
    {
        Task<bool> ClearCart(string userId);
        Task<CartDto> CreateUpdateCart(CartDto cartDto);
        Task<CartDto> GetCartByUserId(string Userid);
        Task<bool> RemoveFromCart(int cartDetailsId);
    }
}