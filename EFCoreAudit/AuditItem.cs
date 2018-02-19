using Microsoft.EntityFrameworkCore.ChangeTracking;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NeuroSpeech.EFCoreAudit
{
    [Table("AuditHistory")]
    public class AuditItem
    {

        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long ID { get; set; }

        [Required, MaxLength(380), Column(Order = 2)]
        public string PrimaryKey { get; set; }

        [Required, MaxLength(50), Column(Order = 3)]
        public string TableName { get; set; }

        public string OldValues { get; set; }

        public string NewValues { get; set; }

        public long? ParentID { get; set; }

        public AuditOperation Operation { get; set; }

        public DateTime Timestamp { get; set; }

        [ForeignKey(nameof(ParentID))]
        [InverseProperty(nameof(Children))]
        public AuditItem Parent { get; set; }

        [InverseProperty(nameof(Parent))]
        public ICollection<AuditItem> Children { get; set; }

        [MaxLength(450)]
        public string UserInfo { get; set; }

        public string Notes { get; set; }

        [MaxLength(100)]
        public string IPAddress { get; set; }

        public long SessionID { get; set; }

        [NotMapped]
        internal EntityEntry Entry { get; set; }
    }
}
