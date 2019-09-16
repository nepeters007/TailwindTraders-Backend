﻿using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Threading.Tasks;
using Tailwind.Traders.Product.Api.Dtos;
using Tailwind.Traders.Product.Api.Extensions;
using Tailwind.Traders.Product.Api.Infrastructure;
using Tailwind.Traders.Product.Api.Mappers;
using Tailwind.Traders.Product.Api.Models;

namespace Tailwind.Traders.Product.Api.Controllers
{
    [Route("v{version:apiVersion}/[controller]")]
    [ApiController]
    [ApiVersion("1.0")]
    public class ProductController : ControllerBase
    {
        private readonly ProductContext _productContext;
        private readonly ILogger<ProductController> _logger;
        private readonly MapperDtos _mapperDtos;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly AppSettings _settings;

        public ProductController(ProductContext productContext, ILogger<ProductController> logger, MapperDtos mapperDtos, IHttpClientFactory httpClientFactory, IOptions<AppSettings> options)
        {
            _productContext = productContext;
            _logger = logger;
            _mapperDtos = mapperDtos;
            _httpClientFactory = httpClientFactory;
            _settings = options.Value;
        }

        [HttpGet]
        [ProducesResponseType(200)]
        [ProducesResponseType(204)]
        public async Task<IActionResult> AllProductsAsync()
        {
            var items = await _productContext.ProductItems.ToListAsync();

            items
                .OrderByDescending(inc => inc.Name.Contains("gnome"))
                .Join(
                _productContext.ProductBrands,
                _productContext.ProductTypes,
                _productContext.ProductFeatures,
                _productContext.Tags);

            if (!items.Any())
            {
                _logger.LogDebug("Products empty");
                return NoContent();
            }

            return Ok(_mapperDtos.MapperToProductDto(items));
        }

        [HttpGet]
        [Route("{productId:int}")]
        [ProducesResponseType(200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> ProductByIdAsync(int productId)
        {

            var items = await _productContext.ProductItems.ToListAsync();

            items.Join(
                _productContext.ProductBrands,
                _productContext.ProductTypes,
                _productContext.ProductFeatures,
                _productContext.Tags);

            //var item = items.Contains(i => i.Id == productId)

            var item = items.Where(p => p.Id == productId).FirstOrDefault();

            //var item = await _productContext.ProductItems
            //    .Include(inc => inc.Brand)
            //    .Include(inc => inc.Features)
            //    .Include(inc => inc.Type)
            //    .FirstOrDefaultAsync(product => product.Id == productId);

            if (item == null)
            {
                _logger.LogDebug($"Product with id '{productId}', not found");

                return NotFound();
            }

            try
            {
                var data = new
                {
                    UserId = ((ClaimsIdentity)User.Identity).Claims.Single(claim => claim.Type == "emails").Value,
                    Product = item
                };
                StringContent dataSerialized = new StringContent(JsonConvert.SerializeObject(data));

                await _httpClientFactory.CreateClient().PostAsync(RoutePathExtensions.VisitsHttpTrigger(_settings.ProductVisitsUrl), dataSerialized)
                .ContinueWith(task =>
                {
                    if (task.IsFaulted)
                    {
                        _logger.LogError("Can not send data to products visits");
                    }
                });
            }
            catch (Exception)
            {
                _logger.LogError($"Call to AF ProductsVisit failed!");

            }

            return Ok(_mapperDtos.MapperToProductDto(item, isDetail: true));
        }

        [HttpGet("filter")]
        [ProducesResponseType(200)]
        [ProducesResponseType(204)]
        public async Task<IActionResult> FindProductAsync([FromQuery] int[] brand, [FromQuery] int[] type)
        {

            var items = await _productContext.ProductItems.ToListAsync();

            items
                .OrderByDescending(inc => inc.Name.Contains("gnome"))
                .Join(
                _productContext.ProductBrands,
                _productContext.ProductTypes,
                _productContext.ProductFeatures,
                _productContext.Tags);


            var itemsToList = items.Where(item => brand.Contains(item.Brand.Id) || type.Contains(item.Type.Id));

            if (!itemsToList.Any())
            {
                _logger.LogDebug("Not Products for this criteria");

                return NoContent();
            }

            return Ok(_mapperDtos.MapperToProductDto(itemsToList));
        }

        [HttpGet("tag/{tag}")]
        [ProducesResponseType(200)]
        [ProducesResponseType(204)]
        public async Task<IActionResult> FindProductsByTag(string tag)
        {
            var productTag = _productContext.Tags.SingleOrDefault(t => t.Value == tag);

            if (productTag == null)
            {
                _logger.LogDebug("No tag found: " + tag);
                return NoContent();
            }

            var items = await _productContext.ProductItems.ToListAsync();

            items = items.Where(p => p.TagId == productTag.Id).Take(3).ToList();

            items.Join(
                _productContext.ProductBrands,
                _productContext.ProductTypes,
                _productContext.ProductFeatures,
                _productContext.Tags);
                
            var data = items.Select(p => new ClassifiedProductDto()
            {
                Id = p.Id,
                ImageUrl = $"{_settings.ProductImagesUrl}/{p.ImageName}",
                Name = p.Name,
                Price = p.Price
            });

            if (!data.Any())
            {
                _logger.LogDebug("No products found with the tag: " + tag);
                return NoContent();
            }

            return Ok(data);
        }

        [HttpGet("recommended")]
        [ProducesResponseType(200)]
        [ProducesResponseType(204)]
        public async Task<IActionResult> RecommendedProductsAsync()
        {
            var items = await _productContext.ProductItems
                .Include(inc => inc.Brand)
                .Include(inc => inc.Features)
                .Include(inc => inc.Type)
                .ToListAsync();

            if (!items.Any())
            {
                _logger.LogDebug("There are no recommended products");

                return NoContent();
            }

            return Ok(_mapperDtos.MapperToProductDto(items).OrderBy(product => new Random().Next()).Take(3));
        }
    }
}