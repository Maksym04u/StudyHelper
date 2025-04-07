using Xunit;
using Moq;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudyApp.Models;
using StudyApp.Data;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using StudyApp.Controllers;

namespace StudyHelper.Tests
{
    public class GradeControllerTests
    {
        private Mock<UserManager<User>> GetMockUserManager()
        {
            var store = new Mock<IUserStore<User>>();
            return new Mock<UserManager<User>>(store.Object, null, null, null, null, null, null, null, null);
        }

        private ApplicationDbContext GetMockDbContext(string databaseName, List<GradeModel> testGrades = null)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: databaseName)
                .Options;

            var context = new ApplicationDbContext(options);
            
            // Clear existing data
            context.Grades.RemoveRange(context.Grades);
            context.Users.RemoveRange(context.Users);
            context.SaveChanges();
            
            if (testGrades != null)
            {
                context.Grades.AddRange(testGrades);
                context.SaveChanges();
            }

            return context;
        }

        private ControllerContext GetMockControllerContext(string userId = "1")
        {
            var claims = new List<Claim>();
            if (userId != null)
            {
                claims.Add(new Claim(ClaimTypes.NameIdentifier, userId));
                claims.Add(new Claim(ClaimTypes.Name, "test@example.com"));
            }

            var user = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuthentication"));

            return new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user }
            };
        }

        [Theory]
        [InlineData(true, "1", "test@example.com", 2)] // Authenticated user with grades
        [InlineData(true, "1", "test@example.com", 0)] // Authenticated user without grades
        [InlineData(false, null, null, 0)] // Unauthenticated user
        public async Task Index_Grade(
            bool isAuthenticated,
            string expectedUserId,
            string expectedUserName,
            int expectedGradeCount)
        {
            // Arrange
            var mockUserManager = GetMockUserManager();
            var testUser = isAuthenticated ? new User { Id = "1", UserName = "test@example.com" } : null;
            
            mockUserManager.Setup(x => x.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                .ReturnsAsync(testUser);

            var testGrades = new List<GradeModel>();
            if (isAuthenticated && expectedGradeCount > 0)
            {
                for (int i = 1; i <= expectedGradeCount; i++)
                {
                    testGrades.Add(new GradeModel 
                    { 
                        Id = i, 
                        Subject = $"Subject {i}", 
                        Grade = i * 10,
                        UserId = expectedUserId,
                        Date = DateTime.Today.AddDays(-i)
                    });
                }
            }

            using var context = GetMockDbContext($"IndexTestDb_{isAuthenticated}", testGrades);
            var controller = new GradeController(context, mockUserManager.Object);
            controller.ControllerContext = GetMockControllerContext();

            // Act
            var result = await controller.Index();

            // Assert
            if (isAuthenticated)
            {
                var viewResult = Assert.IsType<ViewResult>(result);
                var model = Assert.IsAssignableFrom<List<GradeModel>>(viewResult.Model);
                Assert.Equal(expectedGradeCount, model.Count);

                if (expectedGradeCount > 0)
                {
                    for (int i = 0; i < expectedGradeCount; i++)
                    {
                        Assert.Equal($"Subject {i + 1}", model[i].Subject);
                        Assert.Equal((i + 1) * 10, model[i].Grade);
                        Assert.Equal(expectedUserId, model[i].UserId);
                        Assert.Equal(DateTime.Today.AddDays(-(i + 1)).Date, model[i].Date.Date);
                    }
                }
    
            }
            else
            {
                var redirectResult = Assert.IsType<RedirectToActionResult>(result);
                Assert.Equal("Login", redirectResult.ActionName);
                Assert.Equal("Account", redirectResult.ControllerName);
            }
        }

        [Fact]
        public void Create_GET_Grade()
        {
            // Arrange
            var mockUserManager = GetMockUserManager();
            using var context = GetMockDbContext("CreateGetTestDb");
            var controller = new GradeController(context, mockUserManager.Object);
            controller.ControllerContext = GetMockControllerContext();

            // Act
            var result = controller.Create();

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<GradeModel>(viewResult.Model);
            Assert.Equal(DateTime.Today, model.Date);
            Assert.Equal(string.Empty, model.Subject); // Default value
            Assert.Equal(0, model.Grade); // Default value
            Assert.Null(model.UserId); // Not set yet
        }

        [Theory]
        [InlineData(true, "1", "test@example.com", "Math", 85, true)] // Valid grade
        [InlineData(true, "1", "test@example.com", "", 85, false)] // Missing subject
        [InlineData(true, "1", "test@example.com", "Math", 0, false)] // Missing grade
        [InlineData(false, null, null, "Math", 85, false)] // Unauthenticated user
        public async Task Create_POST_Grade(
            bool isAuthenticated, 
            string expectedUserId, 
            string expectedUserName,
            string subject,
            double grade,
            bool shouldSucceed)
        {
            // Arrange
            var mockUserManager = GetMockUserManager();
            var testUser = isAuthenticated ? new User { Id = expectedUserId, UserName = expectedUserName } : null;
            
            mockUserManager.Setup(x => x.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                .ReturnsAsync(testUser);

            using var context = GetMockDbContext($"CreatePostTestDb_{isAuthenticated}_{subject}_{grade}");
            
            // Add the test user to the database if authenticated
            if (isAuthenticated)
            {
                context.Users.Add(testUser);
                await context.SaveChangesAsync();
            }

            var controller = new GradeController(context, mockUserManager.Object);
            controller.ControllerContext = GetMockControllerContext(expectedUserId);

            // Add model validation errors if needed
            if (!shouldSucceed && isAuthenticated)
            {
                if (string.IsNullOrEmpty(subject))
                {
                    controller.ModelState.AddModelError("Subject", "Предмет є обов'язковим.");
                }
                if (grade == 0)
                {
                    controller.ModelState.AddModelError("Grade", "Оцінка є обов'язковою.");
                }
            }

            var newGrade = new GradeModel
            {
                Subject = subject,
                Grade = grade,
                Date = DateTime.Today,
                UserId = expectedUserId,
                User = testUser // Set the navigation property
            };

            // Act
            var result = await controller.Create(newGrade);

            // Assert
            if (!isAuthenticated)
            {
                var redirectResult = Assert.IsType<RedirectToActionResult>(result);
                Assert.Equal("Login", redirectResult.ActionName);
                Assert.Equal("Account", redirectResult.ControllerName);
                return;
            }

            if (shouldSucceed)
            {
                var redirectResult = Assert.IsType<RedirectToActionResult>(result);
                Assert.Equal("Index", redirectResult.ActionName);

                // Verify the grade was created with correct user data
                var createdGrade = await context.Grades
                    .Include(g => g.User) // Include the User navigation property
                    .FirstOrDefaultAsync(g => g.Subject == subject);
                
                Assert.NotNull(createdGrade);
                Assert.Equal(expectedUserId, createdGrade.UserId);
                Assert.Equal(subject, createdGrade.Subject);
                Assert.Equal(grade, createdGrade.Grade);
                Assert.Equal(DateTime.Today, createdGrade.Date);
                
                // Verify the User navigation property
                Assert.NotNull(createdGrade.User);
                Assert.Equal(expectedUserId, createdGrade.User.Id);
                Assert.Equal(expectedUserName, createdGrade.User.UserName);
            }
            else
            {
                // For validation failures, we expect to return to the view
                var viewResult = Assert.IsType<ViewResult>(result);
                Assert.Equal(newGrade, viewResult.Model);
                Assert.False(controller.ModelState.IsValid);
                
                // Verify no grade was created
                var createdGrade = context.Grades.FirstOrDefault(g => g.Subject == subject);
                Assert.Null(createdGrade);
            }
        }

        [Theory]
        [InlineData(1, true, "1")] // Valid grade ID, same user
        [InlineData(999, false, "1")] // Invalid grade ID
        [InlineData(1, false, "2")] // Valid grade ID but different user
        [InlineData(null, false, "1")] // Null grade ID
        public async Task Edit_GET_Grade(int? gradeId, bool shouldSucceed, string userId = "1")
        {
            // Arrange
            var mockUserManager = GetMockUserManager();
            var testUser = new User { Id = userId, UserName = "test@example.com" };
            
            mockUserManager.Setup(x => x.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                .ReturnsAsync(testUser);

            var testGrades = new List<GradeModel>
            {
                new GradeModel 
                { 
                    Id = 1, 
                    Subject = "Math", 
                    Grade = 85,
                    UserId = "1",
                    Date = DateTime.Today
                }
            };

            using var context = GetMockDbContext($"EditGetTestDb_{gradeId}_{userId}", testGrades);
            var controller = new GradeController(context, mockUserManager.Object);
            controller.ControllerContext = GetMockControllerContext(userId);

            // Act
            var result = await controller.Edit(gradeId);

            // Assert
            if (shouldSucceed)
            {
                var viewResult = Assert.IsType<ViewResult>(result);
                var model = Assert.IsType<GradeModel>(viewResult.Model);
                Assert.Equal(gradeId, model.Id);
                Assert.Equal("Math", model.Subject);
                Assert.Equal(85, model.Grade);
                Assert.Equal(DateTime.Today, model.Date);
                Assert.Equal("1", model.UserId);
            }
            else
            {
                if (gradeId == null)
                {
                    var notFoundResult = Assert.IsType<NotFoundResult>(result);
                }
                else
                {
                    var notFoundResult = Assert.IsType<NotFoundResult>(result);
                }
            }
        }

        [Theory]
        [InlineData(1, true, "1", "Updated Math", 90)] // Valid update
        [InlineData(1, false, "1", "", 90)] // Missing subject
        [InlineData(1, false, "1", "Updated Math", 0)] // Missing grade
        [InlineData(999, false, "1", "Updated Math", 90)] // Invalid grade ID
        [InlineData(1, false, "2", "Updated Math", 90)] // Different user
        public async Task Edit_POST_Grade(
            int gradeId,
            bool shouldSucceed,
            string userId,
            string subject,
            double grade)
        {
            // Arrange
            var mockUserManager = GetMockUserManager();
            var testUser = new User { Id = userId, UserName = "test@example.com" };
            
            mockUserManager.Setup(x => x.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                .ReturnsAsync(testUser);

            var testGrades = new List<GradeModel>
            {
                new GradeModel 
                { 
                    Id = 1, 
                    Subject = "Math", 
                    Grade = 85,
                    UserId = "1",
                    Date = DateTime.Today
                }
            };

            using var context = GetMockDbContext($"EditPostTestDb_{gradeId}_{userId}", testGrades);
            var controller = new GradeController(context, mockUserManager.Object);
            controller.ControllerContext = GetMockControllerContext(userId);

            // Add model validation errors if needed
            if (!shouldSucceed && userId == "1" && gradeId == 1)
            {
                if (string.IsNullOrEmpty(subject))
                {
                    controller.ModelState.AddModelError("Subject", "Предмет є обов'язковим.");
                }
                if (grade == 0)
                {
                    controller.ModelState.AddModelError("Grade", "Оцінка є обов'язковою.");
                }
            }

            var updatedGrade = new GradeModel
            {
                Id = gradeId,
                Subject = subject,
                Grade = grade,
                Date = DateTime.Today.AddDays(1),
                UserId = userId
            };

            // Act
            var result = await controller.Edit(gradeId, updatedGrade);

            // Assert
            if (shouldSucceed)
            {
                var redirectResult = Assert.IsType<RedirectToActionResult>(result);
                Assert.Equal("Index", redirectResult.ActionName);

                // Verify the grade was updated
                var gradeInDb = await context.Grades.FindAsync(gradeId);
                Assert.NotNull(gradeInDb);
                Assert.Equal(subject, gradeInDb.Subject);
                Assert.Equal(grade, gradeInDb.Grade);
                Assert.Equal(DateTime.Today.AddDays(1).Date, gradeInDb.Date.Date);
                Assert.Equal("1", gradeInDb.UserId); // UserId should not change
            }
            else
            {
                if (userId != "1" || gradeId != 1)
                {
                    var notFoundResult = Assert.IsType<NotFoundResult>(result);
                }
                else
                {
                    // For validation failures, we expect to return to the view
                    var viewResult = Assert.IsType<ViewResult>(result);
                    Assert.Equal(updatedGrade, viewResult.Model);
                    Assert.False(controller.ModelState.IsValid);

                    // Verify the grade was not updated
                    var gradeInDb = await context.Grades.FindAsync(gradeId);
                    Assert.NotNull(gradeInDb);
                    Assert.Equal("Math", gradeInDb.Subject);
                    Assert.Equal(85, gradeInDb.Grade);
                    Assert.Equal(DateTime.Today.Date, gradeInDb.Date.Date);
                }
            }
        }

        [Theory]
        [InlineData(1, true, "1")] // Valid grade ID, same user
        [InlineData(999, false, "1")] // Invalid grade ID
        [InlineData(1, false, "2")] // Valid grade ID but different user
        public async Task Delete_POST_Grade(int gradeId, bool shouldSucceed, string userId = "1")
        {
            // Arrange
            var mockUserManager = GetMockUserManager();
            var testUser = new User { Id = userId, UserName = "test@example.com" };
            
            mockUserManager.Setup(x => x.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                .ReturnsAsync(testUser);

            var testGrades = new List<GradeModel>
            {
                new GradeModel 
                { 
                    Id = 1, 
                    Subject = "Math", 
                    Grade = 85,
                    UserId = "1",
                    Date = DateTime.Today
                }
            };

            using var context = GetMockDbContext($"DeleteTestDb_{gradeId}_{userId}", testGrades);
            var controller = new GradeController(context, mockUserManager.Object);
            controller.ControllerContext = GetMockControllerContext(userId);

            // Act
            var result = await controller.Delete(gradeId);

            // Assert
            if (shouldSucceed)
            {
                var redirectResult = Assert.IsType<RedirectToActionResult>(result);
                Assert.Equal("Index", redirectResult.ActionName);

                // Verify the grade was deleted
                var gradeInDb = await context.Grades.FindAsync(gradeId);
                Assert.Null(gradeInDb);
            }
            else
            {
                var notFoundResult = Assert.IsType<NotFoundResult>(result);

                // Verify the grade was not deleted
                var gradeInDb = await context.Grades.FindAsync(gradeId);
                if (gradeId == 1) // Only check if it's a valid grade ID
                {
                    Assert.NotNull(gradeInDb);
                    Assert.Equal("Math", gradeInDb.Subject);
                    Assert.Equal(85, gradeInDb.Grade);
                    Assert.Equal(DateTime.Today, gradeInDb.Date);
                    Assert.Equal("1", gradeInDb.UserId);
                }
            }
        }
    }
}
