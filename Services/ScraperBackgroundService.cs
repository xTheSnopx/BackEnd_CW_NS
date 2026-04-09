using CWNS.BackEnd.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace CWNS.BackEnd.Services
{
    public class NinjaSagaMember
    {
        public string name { get; set; } = string.Empty;
        public long reputation { get; set; }
    }

    public class NinjaSagaClan
    {
        public int rank { get; set; }
        public string name { get; set; } = string.Empty;
        public int members { get; set; }
        public long reputation { get; set; }
        public List<NinjaSagaMember>? member_list { get; set; }
    }

    public class NinjaSagaResponse
    {
        public List<NinjaSagaClan>? clans { get; set; }
    }

    public class ScraperBackgroundService : BackgroundService
    {
        private readonly ILogger<ScraperBackgroundService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly HttpClient _httpClient;
        private readonly Dictionary<string, long> _lastMemberPoints = new();

        public ScraperBackgroundService(ILogger<ScraperBackgroundService> logger, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(15);
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json, text/plain, */*");
            _httpClient.DefaultRequestHeaders.Add("Referer", "https://ninjasaga.cc/");
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("ScraperBackgroundService started - Fetching real data from NinjaSaga every 5 seconds");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    var url = $"https://ninjasaga.cc/data/clan_rankings.json?t={timestamp}";
                    
                    var response = await _httpClient.GetAsync(url, stoppingToken);
                    if (response.IsSuccessStatusCode)
                    {
                        var jsonString = await response.Content.ReadAsStringAsync(stoppingToken);
                        var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                        var data = System.Text.Json.JsonSerializer.Deserialize<NinjaSagaResponse>(jsonString, options);

                        if (data?.clans != null)
                        {
                            using (var scope = _serviceProvider.CreateScope())
                            {
                                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                                var users = await context.Users.ToListAsync(stoppingToken);
                                bool madeChanges = false;

                                foreach (var clan in data.clans)
                                {
                                    if (clan.member_list != null)
                                    {
                                        foreach (var member in clan.member_list)
                                        {
                                            // Check anti-bloat cache
                                            if (!_lastMemberPoints.ContainsKey(member.name) || _lastMemberPoints[member.name] != member.reputation)
                                            {
                                                _lastMemberPoints[member.name] = member.reputation;

                                                // Update specific User (Dashboard Projection)
                                                var matchedUser = users.FirstOrDefault(u => u.Username.Equals(member.name, StringComparison.OrdinalIgnoreCase));
                                                if (matchedUser != null)
                                                {
                                                    context.ReputationLogs.Add(new Models.ReputationLog 
                                                    { 
                                                        UserId = matchedUser.Id, 
                                                        Points = (int)member.reputation,
                                                        Timestamp = DateTime.UtcNow
                                                    });
                                                }

                                                // Update general Member Log
                                                context.MemberReputationLogs.Add(new Models.MemberReputationLog
                                                {
                                                    ClanId = clan.name,
                                                    MemberName = member.name,
                                                    Points = (int)member.reputation,
                                                    Timestamp = DateTime.UtcNow
                                                });
                                                
                                                madeChanges = true;
                                            }
                                        }
                                    }
                                }
                                
                                if (madeChanges)
                                {
                                    await context.SaveChangesAsync(stoppingToken);
                                    _logger.LogInformation("Saved new reputation deltas to database.");
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred executing scraper.");
                }

                // Aggressive 5 seconds refresh for instant tactical hoverboard updates
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
}
