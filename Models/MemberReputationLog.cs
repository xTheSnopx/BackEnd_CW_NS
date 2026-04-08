using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CWNS.BackEnd.Models
{
    public class MemberReputationLog
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        public string ClanId { get; set; } = string.Empty;
        
        [Required]
        public string MemberName { get; set; } = string.Empty;

        public int? SeasonId { get; set; }
        [ForeignKey("SeasonId")]
        public Season? Season { get; set; }

        public int Points { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
