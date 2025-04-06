using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace StudyApp.Models
{
    public class Study
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Title { get; set; }

        [Required]
        public string UserId { get; set; }

        [ForeignKey("UserId")]
        public User User { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public string? Category { get; set; }
        public string? Tags { get; set; }

        public List<StudyFile> Files { get; set; } = new();
    }

    public class StudyFile
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string OriginalName { get; set; }

        [Required]
        public string StoragePath { get; set; }

        public string FileType { get; set; }
        public long FileSize { get; set; }
        public string? Description { get; set; }
        public DateTime UploadDate { get; set; } = DateTime.Now;

        [Required]
        public int StudyId { get; set; }

        [ForeignKey("StudyId")]
        public Study Study { get; set; }
    }
}