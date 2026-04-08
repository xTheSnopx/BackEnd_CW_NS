using CWNS.BackEnd.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CWNS.BackEnd.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DataController : ControllerBase
    {
        private readonly AppDbContext _context;

        public DataController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet("history")]
        [Authorize]
        public async Task<IActionResult> GetHistory()
        {
            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "id")?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                return Unauthorized();

            var logs = await _context.ReputationLogs
                .Where(l => l.UserId == userId)
                .OrderByDescending(l => l.Timestamp)
                .Take(50)
                .ToListAsync();

            return Ok(logs);
        }

        [HttpGet("rankings")]
        public IActionResult GetRankings()
        {
            // Usually this would return cache from the ScraperService
            // For now, return mock empty or placeholder logic
            var mockRankings = new[]
            {
                new { rank = 1, clanName = "Ninja", reputation = 1500000 },
                new { rank = 2, clanName = "Akatsuki", reputation = 1200000 }
            };
            return Ok(mockRankings);
        }

        [HttpGet("clan-gains")]
        public async Task<IActionResult> GetClanGains([FromQuery] string clanId)
        {
            if (string.IsNullOrWhiteSpace(clanId))
                return BadRequest(new { error = "clanId is required" });

            var gains = await _context.MemberReputationLogs
                .Where(l => l.ClanId == clanId)
                .GroupBy(l => l.MemberName)
                .Select(g => new
                {
                    MemberName = g.Key,
                    LatestPoints = g.OrderByDescending(x => x.Timestamp).Select(x => x.Points).FirstOrDefault(),
                    TotalGain = 0 // Needs complex lag/lead logic, simplify for now
                })
                .ToListAsync();

            return Ok(gains);
        }
    }
}
