using DeTaiNhanSu.DbContextProject;
using DeTaiNhanSu.Dtos.Notification;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DeTaiNhanSu.Controllers.Notification
{
    [ApiController]
    [Route("api/[controller]")]
    public class FcmTokenController : Controller
    {
        private readonly AppDbContext _context;

        public FcmTokenController(AppDbContext context)
        {
            _context = context;
        }

        [HttpPost("save")]
        public async Task<IActionResult> SaveToken([FromBody] FcmToken tokenModel)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(tokenModel.Token))
                    return BadRequest("Token is required");

                var exists = await _context.FcmTokens
                    .AnyAsync(x => x.Token == tokenModel.Token && x.UserId == tokenModel.UserId);

                if (!exists)
                {
                    _context.FcmTokens.Add(tokenModel);
                    await _context.SaveChangesAsync();
                }

                return Ok("Token saved or already exists");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi khi lưu:{ex}");
                return StatusCode(500, "An error occurred while saving the token");
            }

        }
    }
}
