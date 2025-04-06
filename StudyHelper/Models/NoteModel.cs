using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;


namespace StudyApp.Models
{
    public class NoteModel
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Заголовок нотатки є обов'язковим.")]
        [StringLength(100, ErrorMessage = "Заголовок не може перевищувати 100 символів.")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "Вміст нотатки є обов'язковим.")]
        public string Note { get; set; } = string.Empty;

        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public DateTime? LastModifiedDate { get; set; }

        [Required]
        public string UserId { get; set; }  

        [ForeignKey("UserId")]
        public string Author { get; set; }  // Navigation property
    }
}