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
        public async Task<IActionResult> GetRankings()
        {
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var url = $"https://ninjasaga.cc/data/clan_rankings.json?t={timestamp}";
            
            try 
            {
                using var client = new HttpClient();
                var response = await client.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var payload = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.Nodes.JsonObject>(content, options);
                    
                    if (payload != null && payload["clans"] is System.Text.Json.Nodes.JsonArray clansArray)
                    {
                        var target6h = DateTime.UtcNow.AddHours(-6);
                        var target24h = DateTime.UtcNow.AddHours(-24);

                        // Optimizado: obtener la última rep registrada ANTES del target6h para CADA miembro
                        var past6hLogs = await _context.MemberReputationLogs
                            .Where(l => l.Timestamp <= target6h)
                            .GroupBy(l => new { l.ClanId, l.MemberName })
                            .Select(g => new { 
                                ClanId = g.Key.ClanId, 
                                Points = g.OrderByDescending(x => x.Timestamp).Select(x => x.Points).FirstOrDefault() 
                            })
                            .ToListAsync();

                        var target2h = DateTime.UtcNow.AddHours(-2);
                        var past2hLogs = await _context.MemberReputationLogs
                            .Where(l => l.Timestamp <= target2h)
                            .GroupBy(l => new { l.ClanId, l.MemberName })
                            .Select(g => new { 
                                ClanId = g.Key.ClanId, 
                                MemberName = g.Key.MemberName,
                                Points = g.OrderByDescending(x => x.Timestamp).Select(x => x.Points).FirstOrDefault() 
                            })
                            .ToListAsync();
                        var clanMemberReps2h = past2hLogs.GroupBy(x => x.ClanId).ToDictionary(g => g.Key, g => g.ToDictionary(m => m.MemberName, m => (long)m.Points));

                        var past24hLogs = await _context.MemberReputationLogs
                            .Where(l => l.Timestamp <= target24h)
                            .GroupBy(l => new { l.ClanId, l.MemberName })
                            .Select(g => new { 
                                ClanId = g.Key.ClanId, 
                                Points = g.OrderByDescending(x => x.Timestamp).Select(x => x.Points).FirstOrDefault() 
                            })
                            .ToListAsync();

                        var clanRep6h = past6hLogs.GroupBy(x => x.ClanId).ToDictionary(g => g.Key, g => g.Sum(x => (long)x.Points));
                        var clanRep24h = past24hLogs.GroupBy(x => x.ClanId).ToDictionary(g => g.Key, g => g.Sum(x => (long)x.Points));

                        foreach (var node in clansArray)
                        {
                            if (node is System.Text.Json.Nodes.JsonObject clanObj)
                            {
                                var clanName = clanObj["name"]?.ToString();
                                var currentRepNode = clanObj["reputation"];
                                long currentRep = currentRepNode != null ? currentRepNode.GetValue<long>() : 0;
                                
                                if (!string.IsNullOrEmpty(clanName))
                                {
                                    long rep6h = clanRep6h.ContainsKey(clanName) ? clanRep6h[clanName] : currentRep; // If 0 tracking history, delta is 0
                                    long rep24h = clanRep24h.ContainsKey(clanName) ? clanRep24h[clanName] : currentRep;
                                    
                                    clanObj["sixHourDelta"] = currentRep - rep6h;
                                    clanObj["twentyFourHourDelta"] = currentRep - rep24h;

                                    int activeCount = 0;
                                    if (clanObj["member_list"] is System.Text.Json.Nodes.JsonArray memberList)
                                    {
                                        var pastMembers = clanMemberReps2h.ContainsKey(clanName) ? clanMemberReps2h[clanName] : new Dictionary<string, long>();
                                        foreach (var mNode in memberList)
                                        {
                                            if (mNode is System.Text.Json.Nodes.JsonObject mObj)
                                            {
                                                var mName = mObj["name"]?.ToString();
                                                var mRepNode = mObj["reputation"];
                                                long mRep = mRepNode != null ? mRepNode.GetValue<long>() : 0;
                                                
                                                if (!string.IsNullOrEmpty(mName))
                                                {
                                                    long pastRep = pastMembers.ContainsKey(mName) ? pastMembers[mName] : 0;
                                                    if (pastRep > 0 && mRep > pastRep)
                                                    {
                                                        activeCount++;
                                                    }
                                                }
                                            }
                                        }
                                    }
                                    clanObj["activeMembers"] = activeCount;
                                }
                            }
                        }
                        
                        return Ok(payload);
                    }
                    
                    return Content(content, "application/json"); 
                }
            } 
            catch (Exception ex)
            {
                Console.WriteLine("API Fetch Failed: " + ex.Message);
            }

            return BadRequest(new { error = "No se pudo recuperar la información de Ninja Saga." });
        }

        [HttpGet("clan-gains")]
        public async Task<IActionResult> GetClanGains([FromQuery] string clanId)
        {
            if (string.IsNullOrWhiteSpace(clanId))
                return BadRequest(new { error = "clanId is required" });

            var target6h = DateTime.UtcNow.AddHours(-6);
            
            var allLogs = await _context.MemberReputationLogs
                .Where(l => l.ClanId == clanId)
                .OrderBy(l => l.Timestamp)
                .ToListAsync();

            var gains = allLogs.GroupBy(l => l.MemberName)
                .Select(g => {
                    var latest = g.Last();
                    var pastLog = g.LastOrDefault(l => l.Timestamp <= target6h) ?? g.First();
                    long prevPoints = pastLog.Points;
                    
                    // If the earliest log we have is still newer than 6 hours ago, 
                    // we assume they gained it since we started tracking, so prevPoints is their first known point.
                    
                    return new {
                        MemberName = latest.MemberName,
                        LatestPoints = latest.Points,
                        PrevPoints = prevPoints,
                        Delta6h = latest.Points - prevPoints
                    };
                })
                .ToList();

            return Ok(gains);
        }
        [HttpGet("notifications")]
        public async Task<IActionResult> GetNotifications()
        {
            var target2h = DateTime.UtcNow.AddHours(-2);
            
            // Get last 2 hours delta to detect recent bleeding
            var pastLogs = await _context.MemberReputationLogs
                .Where(l => l.Timestamp <= target2h)
                .GroupBy(l => new { l.ClanId, l.MemberName })
                .Select(g => new { 
                    ClanId = g.Key.ClanId, 
                    Points = g.OrderByDescending(x => x.Timestamp).Select(x => x.Points).FirstOrDefault() 
                })
                .ToListAsync();

            var currentLogs = await _context.MemberReputationLogs
                .GroupBy(l => new { l.ClanId, l.MemberName })
                .Select(g => new { 
                    ClanId = g.Key.ClanId, 
                    Points = g.OrderByDescending(x => x.Timestamp).Select(x => x.Points).FirstOrDefault() 
                })
                .ToListAsync();

            var clanRepPast = pastLogs.GroupBy(x => x.ClanId).ToDictionary(g => g.Key, g => g.Sum(x => (long)x.Points));
            var clanRepCurrent = currentLogs.GroupBy(x => x.ClanId).ToDictionary(g => g.Key, g => g.Sum(x => (long)x.Points));

            var notifications = new List<object>();

            // If we don't have past logs, it means the server just started.
            if (!clanRepPast.Any())
            {
                notifications.Add(new { msg = "SYSTEM: Escaner iniciado. Mapeando red...", time = "Ahora", type = "info" });
                return Ok(notifications);
            }

            int topActiveCount = 0;
            long activeSum = 0;

            var deltas = new Dictionary<string, long>();
            foreach(var kv in clanRepCurrent)
            {
                long past = clanRepPast.ContainsKey(kv.Key) ? clanRepPast[kv.Key] : kv.Value;
                long delta = kv.Value - past;
                deltas[kv.Key] = delta;
                
                if (delta > 1000) 
                {
                    topActiveCount++;
                    activeSum += delta;
                }
            }

            // Logic: if other clans are moving, alert about zero-growth top clans.
            if (topActiveCount >= 3)
            {
                foreach(var clan in deltas)
                {
                    // If a clan has literally 0 or very low delta while the war is active.
                    if (clan.Value <= 0)
                    {
                        notifications.Add(new { 
                            msg = $"[ADVERTENCIA] {clan.Key} está inactivo (Bleeding). ¡Cero rep en 2 horas!", 
                            time = "Reciente", 
                            type = "warning",
                            clan = clan.Key
                        });
                    }
                }
            }

            notifications.Add(new { msg = $"INFO: {topActiveCount} clanes activos en combate.", time = "Reciente", type = "success" });

            return Ok(notifications);
        }
    }
}
