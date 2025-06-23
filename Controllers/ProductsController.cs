using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Data;
using WebApplication1.Models;
using WebApplication1.Services;

namespace WebApplication1.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProductsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ICacheService _cache;
        private const string CACHE_KEY_PREFIX = "product:";
        private const string CACHE_KEY_ALL = "products:all";

        public ProductsController(ApplicationDbContext context, ICacheService cache)
        {
            _context = context;
            _cache = cache;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Product>>> GetProducts()
        {
            var cachedProducts = await _cache.GetAsync<List<Product>>(CACHE_KEY_ALL);
            if (cachedProducts != null)
            {
                return Ok(cachedProducts);
            }

            var products = await _context.Products.ToListAsync();
            await _cache.SetAsync(CACHE_KEY_ALL, products, TimeSpan.FromMinutes(5));
            
            return Ok(products);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Product>> GetProduct(int id)
        {
            var cacheKey = $"{CACHE_KEY_PREFIX}{id}";
            var cachedProduct = await _cache.GetAsync<Product>(cacheKey);
            if (cachedProduct != null)
            {
                return Ok(cachedProduct);
            }

            var product = await _context.Products.FindAsync(id);
            if (product == null)
            {
                return NotFound();
            }

            await _cache.SetAsync(cacheKey, product, TimeSpan.FromMinutes(5));
            return Ok(product);
        }

        [HttpPost]
        public async Task<ActionResult<Product>> CreateProduct(Product product)
        {
            product.CreatedAt = DateTime.UtcNow;
            product.UpdatedAt = DateTime.UtcNow;

            _context.Products.Add(product);
            await _context.SaveChangesAsync();

            await _cache.RemoveAsync(CACHE_KEY_ALL);

            return CreatedAtAction(nameof(GetProduct), new { id = product.Id }, product);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateProduct(int id, Product product)
        {
            if (id != product.Id)
            {
                return BadRequest();
            }

            var existingProduct = await _context.Products.FindAsync(id);
            if (existingProduct == null)
            {
                return NotFound();
            }

            existingProduct.Name = product.Name;
            existingProduct.Description = product.Description;
            existingProduct.Price = product.Price;
            existingProduct.Stock = product.Stock;
            existingProduct.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            await _cache.RemoveAsync($"{CACHE_KEY_PREFIX}{id}");
            await _cache.RemoveAsync(CACHE_KEY_ALL);

            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteProduct(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null)
            {
                return NotFound();
            }

            _context.Products.Remove(product);
            await _context.SaveChangesAsync();

            await _cache.RemoveAsync($"{CACHE_KEY_PREFIX}{id}");
            await _cache.RemoveAsync(CACHE_KEY_ALL);

            return NoContent();
        }
    }
}