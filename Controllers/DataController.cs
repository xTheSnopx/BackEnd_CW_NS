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
            // Providing rich mock data for the Ranking View with realistic Ninja Saga clan names
            var clansList = new[]
            {
                new { rank = 1, name = "Emblem Elite", reputation = 5420000, members = 40 },
                new { rank = 2, name = "Akatsuki", reputation = 4850000, members = 40 },
                new { rank = 3, name = "Jirocho Ninja", reputation = 4200000, members = 38 },
                new { rank = 4, name = "Sannin", reputation = 3800000, members = 40 },
                new { rank = 5, name = "Espíritu Latino", reputation = 3500000, members = 35 },
                new { rank = 6, name = "Uchiha Clan", reputation = 3100000, members = 40 },
                new { rank = 7, name = "Senju Blood", reputation = 2950000, members = 39 },
                new { rank = 8, name = "Shadow ANBU", reputation = 2800000, members = 36 },
                new { rank = 9, name = "Dark Brotherhood", reputation = 2650000, members = 40 },
                new { rank = 10, name = "Kage Masters", reputation = 2400000, members = 34 },
                new { rank = 11, name = "Seven Swordsmen", reputation = 2100000, members = 25 },
                new { rank = 12, name = "Byakugan Eyes", reputation = 1950000, members = 40 },
                new { rank = 13, name = "Taka", reputation = 1800000, members = 20 },
                new { rank = 14, name = "Sound Ninja", reputation = 1650000, members = 38 },
                new { rank = 15, name = "Sand Village", reputation = 1500000, members = 40 },
                new { rank = 16, name = "Kara", reputation = 1450000, members = 30 },
                new { rank = 17, name = "Rogue Ninjas", reputation = 1300000, members = 28 },
                new { rank = 18, name = "Moon Tsukuyomi", reputation = 1200000, members = 35 },
                new { rank = 19, name = "Iron Samurai", reputation = 1100000, members = 40 },
                new { rank = 20, name = "Crystal Arts", reputation = 950000, members = 22 }
            };

            return Ok(new 
            {
                season = "Temporada 2",
                status = "Active",
                clans = clansList
            });
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
