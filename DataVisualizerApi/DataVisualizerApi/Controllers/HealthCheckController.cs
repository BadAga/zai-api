using DataVisualizerApi.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DataVisualizerApi.Controllers
{
    [ApiController]
    [Route("health")]
    public class HealthController : ControllerBase
    {
        private readonly AppDbContext _context;

        public HealthController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult Get() => Ok("OK from API");

        [HttpGet("db")]
        public async Task<IActionResult> CheckDb()
        {
            try
            {
                // Simple query – adjust "Series" to an existing DbSet
                var count = await _context.Series.CountAsync();
                return Ok(new { message = "DB OK", seriesCount = count });
            }
            catch (Exception ex)
            {
                // TEMPORARY: return the error so we see what's wrong on Azure
                return StatusCode(500, new
                {
                    message = "DB ERROR",
                    error = ex.Message,
                    inner = ex.InnerException?.Message
                });
            }
        }
    }
}
