using System;
using System.ComponentModel.DataAnnotations;

namespace CWNS.BackEnd.Models
{
    public class User
    {
        [Key]
        public int Id { get; set; }
        [Required]
        public string Username { get; set; } = string.Empty;
        [Required]
        public string Password { get; set; } = string.Empty;
        public string? ClanId { get; set; }
        public string? MemberId { get; set; }
        public DateTime? PlanExpiresAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
