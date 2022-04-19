using AutoMapper;
using Mango.Services.ProductAPI.DbContexts.Models;

namespace Mango.Services.ProductAPI
{
    public class MappingConfig
    {
        public static MapperConfiguration RegisterMaps()
        {
            var mappingConfig = new MapperConfiguration(config =>
            {
                config.CreateMap<ProductDto, Product>().ReverseMap(); // ReverseMap hace config.CreateMap<Product, ProductDto>()
            });

            return mappingConfig;
        }
    }
}
