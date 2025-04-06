using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudyApp.Data;
using StudyApp.Models;
using StudyApp.Models.ViewModels;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace StudyApp.Controllers
{
    [Authorize]
    public class StudyController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly IWebHostEnvironment _env;

        public StudyController(
            ApplicationDbContext context,
            UserManager<User> userManager,
            IWebHostEnvironment env)
        {
            _context = context;
            _userManager = userManager;
            _env = env;
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var studies = await _context.Studies
                .Include(s => s.Files)
                .Where(s => s.UserId == user.Id)
                .OrderByDescending(s => s.CreatedAt)
                .ToListAsync();

            return View(studies);
        }

[HttpGet]
public IActionResult Create()
{
    return View(new StudyUploadViewModel());
}

[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Create(StudyUploadViewModel viewModel)
{
    if (ModelState.IsValid)
    {
        var study = new Study
        {
            Title = viewModel.Title,
            Category = viewModel.Category,
            Tags = viewModel.Tags,
            CreatedAt = DateTime.Now,
            UserId = _userManager.GetUserId(User)
        };

        // Обробка файлів
 if (viewModel.Files != null && viewModel.Files.Count > 0)
                {
                    foreach (var file in viewModel.Files)
                    {
                        if (file.Length > 0)
                        {
                            var uploadsDir = Path.Combine(_env.WebRootPath, "uploads");
                            Directory.CreateDirectory(uploadsDir);

                            var uniqueName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
                            var filePath = Path.Combine(uploadsDir, uniqueName);

                            using (var stream = new FileStream(filePath, FileMode.Create))
                            {
                                await file.CopyToAsync(stream);
                            }

                            study.Files.Add(new StudyFile
                            {
                                OriginalName = file.FileName,
                                StoragePath = $"/uploads/{uniqueName}",
                                FileType = Path.GetExtension(file.FileName),
                                FileSize = file.Length
                            });
                        }
                    }
                }


        _context.Studies.Add(study);
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }
    return View(viewModel);
}
// [HttpPost]
// [ValidateAntiForgeryToken]
// public async Task<IActionResult> Create(Study study, List<IFormFile> files)
// {
//     if (ModelState.IsValid)
//     {
//         // Обробка файлів та збереження
//         study.CreatedAt = DateTime.Now;
//         _context.Studies.Add(study);
//         await _context.SaveChangesAsync();
//         return RedirectToAction(nameof(Index));
//     }
//     return View(study);
// }

        // [HttpGet]
        // public IActionResult Create()
        // {
        //     return View(new StudyUploadViewModel());
        // }

        // [HttpPost]
        // [ValidateAntiForgeryToken]
        // public async Task<IActionResult> Create(StudyUploadViewModel model)
        // {
        //     var user = await _userManager.GetUserAsync(User);
        //     if (user == null) return Challenge();

        //     if (ModelState.IsValid)
        //     {
        //         var study = new Study
        //         {
        //             Title = model.Title,
        //             Category = model.Category,
        //             Tags = model.Tags,
        //             UserId = user.Id,
        //             User = user
        //         };

        //         if (model.Files != null && model.Files.Count > 0)
        //         {
        //             foreach (var file in model.Files)
        //             {
        //                 if (file.Length > 0)
        //                 {
        //                     var uploadsDir = Path.Combine(_env.WebRootPath, "uploads");
        //                     Directory.CreateDirectory(uploadsDir);

        //                     var uniqueName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
        //                     var filePath = Path.Combine(uploadsDir, uniqueName);

        //                     using (var stream = new FileStream(filePath, FileMode.Create))
        //                     {
        //                         await file.CopyToAsync(stream);
        //                     }

        //                     study.Files.Add(new StudyFile
        //                     {
        //                         OriginalName = file.FileName,
        //                         StoragePath = $"/uploads/{uniqueName}",
        //                         FileType = Path.GetExtension(file.FileName),
        //                         FileSize = file.Length
        //                     });
        //                 }
        //             }
        //         }

        //         _context.Studies.Add(study);
        //         await _context.SaveChangesAsync();

        //         return RedirectToAction(nameof(Index));
        //     }

        //     return View(model);
        // }

   [HttpPost, ActionName("Delete")]
[ValidateAntiForgeryToken]
public async Task<IActionResult> DeleteConfirmed(int id)
{
    var study = await _context.Studies
        .Include(s => s.Files)
        .FirstOrDefaultAsync(s => s.Id == id);

    if (study == null)
    {
        return NotFound();
    }

    foreach (var file in study.Files)
    {
        var filePath = Path.Combine(_env.WebRootPath, file.StoragePath.TrimStart('/'));
        if (System.IO.File.Exists(filePath))
        {
            System.IO.File.Delete(filePath);
        }
    }

    _context.Studies.Remove(study);
    await _context.SaveChangesAsync();

    return RedirectToAction(nameof(Index));
}


[HttpGet]
public async Task<IActionResult> Delete(int? id)
{
    if (id == null)
    {
        return NotFound();
    }

    var study = await _context.Studies
        .Include(s => s.Files)
        .FirstOrDefaultAsync(m => m.Id == id);

    if (study == null)
    {
        return NotFound();
    }

    return View(study);
}

[HttpGet] 
public async Task<IActionResult> Edit(int? id)
{
    if (id == null)
    {
        return NotFound();
    }

    var study = await _context.Studies
        .Include(s => s.Files)
        .FirstOrDefaultAsync(s => s.Id == id);

    if (study == null)
    {
        return NotFound();
    }

    return View(study);
}


[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Edit(int id, Study study, List<IFormFile> newFiles)
{
    if (id != study.Id)
    {
        return NotFound();
    }

    var existingStudy = await _context.Studies
        .Include(s => s.Files)
        .FirstOrDefaultAsync(s => s.Id == id);

    if (existingStudy == null)
    {
        return NotFound();
    }

    if (ModelState.IsValid)
    {
        try
        {
            // Оновлюємо основні поля
            existingStudy.Title = study.Title;
            existingStudy.Category = study.Category;
            existingStudy.Tags = study.Tags;

            // Обробка нових файлів
            if (newFiles != null && newFiles.Count > 0)
            {
                var uploadsDir = Path.Combine(_env.WebRootPath, "uploads");
                if (!Directory.Exists(uploadsDir))
                {
                    Directory.CreateDirectory(uploadsDir);
                }

                foreach (var file in newFiles.Where(f => f.Length > 0))
                {
                    var uniqueFileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
                    var filePath = Path.Combine(uploadsDir, uniqueFileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await file.CopyToAsync(fileStream);
                    }

                    existingStudy.Files.Add(new StudyFile
                    {
                        OriginalName = file.FileName,
                        StoragePath = $"/uploads/{uniqueFileName}",
                        FileType = Path.GetExtension(file.FileName).ToLower(),
                        FileSize = file.Length,
                        UploadDate = DateTime.Now
                    });
                }
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            ModelState.AddModelError("", "Помилка при збереженні: " + ex.Message);
        }
    }

    return View(existingStudy);
}
[HttpPost]
public async Task<IActionResult> DeleteFile(int id)
{
    var file = await _context.StudyFiles
        .Include(f => f.Study)
        .FirstOrDefaultAsync(f => f.Id == id);

    if (file == null)
    {
        return NotFound();
    }

    var physicalPath = Path.Combine(_env.WebRootPath, file.StoragePath.TrimStart('/'));
    if (System.IO.File.Exists(physicalPath))
    {
        System.IO.File.Delete(physicalPath);
    }

    _context.StudyFiles.Remove(file);
    await _context.SaveChangesAsync();

    return Ok();
}
private bool StudyExists(int id)
{
    return _context.Studies.Any(e => e.Id == id);
}
}}