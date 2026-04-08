using Microsoft.EntityFrameworkCore;
using CWNS.BackEnd.Models;

namespace CWNS.BackEnd.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Season> Seasons { get; set; }
        public DbSet<ReputationLog> ReputationLogs { get; set; }
        public DbSet<MemberReputationLog> MemberReputationLogs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            
            // Map explicitly to lower-case table names if you want to match previous Postgres/MySQL schema habits
            modelBuilder.Entity<User>().ToTable("users");
            modelBuilder.Entity<Season>().ToTable("seasons");
            modelBuilder.Entity<ReputationLog>().ToTable("reputation_logs");
            modelBuilder.Entity<MemberReputationLog>().ToTable("member_reputation_logs");
        }
    }
}
