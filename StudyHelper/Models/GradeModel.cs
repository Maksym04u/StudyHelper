using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace StudyApp.Models
{
    public class GradeModel
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Предмет є обов'язковим.")]
        public string Subject { get; set; } = string.Empty;

        [Required(ErrorMessage = "Оцінка є обов'язковою.")]
        public double Grade { get; set; }

        [Required(ErrorMessage = "Дата є обов'язковою.")]
        public DateTime Date { get; set; } = DateTime.Today;

        public string? UserId { get; set; }

        // Navigation property
        [ForeignKey("UserId")]
        public virtual User? User { get; set; }
    }
}
