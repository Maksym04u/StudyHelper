using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudyApp.Data;
using StudyApp.Models;

namespace StudyApp.Controllers
{
    [Authorize]
    public class GradeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;

        public GradeController(ApplicationDbContext context, UserManager<User> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var grades = await _context.Grades
                .Where(g => g.UserId == user.Id)
                .ToListAsync();

            return View(grades);
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View(new GradeModel { Date = DateTime.Today });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(GradeModel gradeModel)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    var user = await _userManager.GetUserAsync(User);
                    if (user == null)
                    {
                        return RedirectToAction("Login", "Account");
                    }

                    gradeModel.UserId = user.Id;
                    _context.Add(gradeModel);
                    await _context.SaveChangesAsync();
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", $"Помилка збереження: {ex.Message}");
                }
            }
            return View(gradeModel);
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var grade = await _context.Grades
                .FirstOrDefaultAsync(g => g.Id == id && g.UserId == user.Id);

            if (grade == null) return NotFound();
            
            return View(grade);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, GradeModel gradeModel)
        {
            if (id != gradeModel.Id) return NotFound();

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var existingGrade = await _context.Grades
                        .FirstOrDefaultAsync(g => g.Id == id && g.UserId == user.Id);

                    if (existingGrade == null)
                    {
                        return NotFound();
                    }

                    existingGrade.Subject = gradeModel.Subject;
                    existingGrade.Grade = gradeModel.Grade;
                    existingGrade.Date = gradeModel.Date;

                    await _context.SaveChangesAsync();
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!GradeExists(gradeModel.Id))
                    {
                        return NotFound();
                    }
                    throw;
                }
            }
            return View(gradeModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var grade = await _context.Grades
                .FirstOrDefaultAsync(g => g.Id == id && g.UserId == user.Id);

            if (grade == null) return NotFound();

            _context.Grades.Remove(grade);
            await _context.SaveChangesAsync();
            
            return RedirectToAction(nameof(Index));
        }

        private bool GradeExists(int id)
        {
            return _context.Grades.Any(e => e.Id == id);
        }
    }
}