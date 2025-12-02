using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TorreClou.Core.Entities;
using TorreClou.Infrastructure.Data;

namespace TorreClou.API.Controllers
{
    [ApiController]
    [Route("api/test")]
    public class InterceptorTestController(ApplicationDbContext context) : ControllerBase
    {
        private readonly ApplicationDbContext _context = context;

        [HttpPost("interceptor")]
        public async Task<IActionResult> TestInterceptor()
        {
            // 1. Create
            var user = new User
            {
                Email = $"test-{Guid.NewGuid()}@example.com",
                FullName = "Test User",
                CreatedAt = DateTime.UtcNow.AddHours(-1) // Set to past to verify it doesn't change on create (or does it? BaseEntity sets it to UtcNow by default, let's see)
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var createdTime = user.CreatedAt;
            var initialUpdatedTime = user.UpdatedAt;

            // 2. Update
            await Task.Delay(1000);
            user.FullName = "Updated Name";
            await _context.SaveChangesAsync();

            var finalUpdatedTime = user.UpdatedAt;

            return Ok(new
            {
                CreatedAt = createdTime,
                InitialUpdatedAt = initialUpdatedTime,
                FinalUpdatedAt = finalUpdatedTime,
                IsUpdated = finalUpdatedTime > createdTime && finalUpdatedTime > initialUpdatedTime
            });
        }
    }
}
