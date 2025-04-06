using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace StudyApp.Models.ViewModels
{
    public class StudyUploadViewModel
    {
        [Required]
        [StringLength(100)]
        public string Title { get; set; }

        public string? Category { get; set; }
        public string? Tags { get; set; }

        [Required]
        public List<IFormFile> Files { get; set; }
    }
}