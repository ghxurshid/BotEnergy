using AdminApi.Extensions;
using AdminApi.Models.Requests;
using Domain.Enums;
using Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AdminApi.Controllers
{
    [Route("api/[controller]/[action]")]
    [ApiController]
    public class ProductAdminController : ControllerBase
    {
        private readonly IProductService _productService;

        public ProductAdminController(IProductService productService)
        {
            _productService = productService;
        }

        [HttpGet]
        public IActionResult GetAllowedTypes([FromQuery] DeviceType deviceType)
        {
            var result = _productService.GetAllowedProductTypes(deviceType);
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, result);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateProductRequest request)
        {
            var result = await _productService.CreateAsync(request.ToDto());
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, result);
        }
    }
}
