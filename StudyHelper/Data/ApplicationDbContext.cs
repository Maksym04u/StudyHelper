using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using StudyApp.Models;

namespace StudyApp.Data
{
    public class ApplicationDbContext : IdentityDbContext<User>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }
        

        public DbSet<TaskModel> Tasks { get; set; }
        public DbSet<GradeModel> Grades { get; set; }
        public DbSet<NoteModel> Notes { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<Study> Studies { get; set; }
        public DbSet<StudyFile> StudyFiles { get; set; }
         protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Study>()
                .HasMany(s => s.Files)
                .WithOne(f => f.Study)
                .HasForeignKey(f => f.StudyId)
                .OnDelete(DeleteBehavior.Cascade);
        }



    }
}
