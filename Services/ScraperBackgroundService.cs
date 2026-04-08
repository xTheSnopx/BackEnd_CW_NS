using CWNS.BackEnd.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace CWNS.BackEnd.Services
{
    public class ScraperBackgroundService : BackgroundService
    {
        private readonly ILogger<ScraperBackgroundService> _logger;
        private readonly IServiceProvider _serviceProvider;

        public ScraperBackgroundService(ILogger<ScraperBackgroundService> logger, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("ScraperBackgroundService started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Scraper executing at: {time}", DateTimeOffset.Now);
                
                try
                {
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                        // Perform scraping and DB logic here
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred executing scraper.");
                }

                // Run every 10 minutes
                await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);
            }
        }
    }
}
