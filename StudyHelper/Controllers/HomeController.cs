using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudyApp.Data;
using StudyApp.Models;
using Microsoft.AspNetCore.Identity;
using System.Threading.Tasks;

namespace StudyApp.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;

        public HomeController(ApplicationDbContext context, UserManager<User> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Tasks()
        {
            return RedirectToAction("Index", "Task");
        }

        public IActionResult Grades()
        {
            return RedirectToAction("Index", "Grade");
        }

        public IActionResult Notes()
        {
            return RedirectToAction("Index", "Note");
        }
        
        public async Task<IActionResult> Studies()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge(); // Перенаправить на сторінку входу, якщо користувач не автентифікований
            }

            // Додаткові дії перед перенаправленням (необов'язково)
            var hasStudies = await _context.Studies.AnyAsync(s => s.UserId == user.Id);
            if (!hasStudies)
            {
                TempData["Message"] = "У вас ще немає конспектів. Створіть перший!";
            }

            return RedirectToAction("Index", "Study");
        }
    }
}