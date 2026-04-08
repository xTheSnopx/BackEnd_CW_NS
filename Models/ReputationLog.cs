using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CWNS.BackEnd.Models
{
    public class ReputationLog
    {
        [Key]
        public int Id { get; set; }
        
        public int UserId { get; set; }
        [ForeignKey("UserId")]
        public User? User { get; set; }

        public int? SeasonId { get; set; }
        [ForeignKey("SeasonId")]
        public Season? Season { get; set; }

        public int Points { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
