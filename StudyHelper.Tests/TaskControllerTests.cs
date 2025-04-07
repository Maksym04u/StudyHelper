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
    public class TaskControllerTests
    {
        private Mock<UserManager<User>> GetMockUserManager()
        {
            var store = new Mock<IUserStore<User>>();
            return new Mock<UserManager<User>>(store.Object, null, null, null, null, null, null, null, null);
        }

        private ApplicationDbContext GetMockDbContext(string databaseName, List<TaskModel> testTasks = null)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: databaseName)
                .Options;

            var context = new ApplicationDbContext(options);
            
            if (testTasks != null)
            {
                context.Tasks.AddRange(testTasks);
                context.SaveChanges();
            }

            return context;
        }

        private ControllerContext GetMockControllerContext(string userId = "1")
        {
            var user = new ClaimsPrincipal(new ClaimsIdentity(new Claim[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Name, "test@example.com")
            }, "TestAuthentication"));

            return new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user }
            };
        }

        [Theory]
        [InlineData(true, "1", "test@example.com", 2)] // Authenticated user with tasks
        [InlineData(true, "1", "test@example.com", 0)] // Authenticated user without tasks
        [InlineData(false, null, null, 0)] // Unauthenticated user
        public async Task Index_Task(
            bool isAuthenticated,
            string expectedUserId,
            string expectedUserName,
            int expectedTaskCount)
        {
            // Arrange
            var mockUserManager = GetMockUserManager();
            var testUser = isAuthenticated ? new User { Id = "1", UserName = "test@example.com" } : null;
            
            mockUserManager.Setup(x => x.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                .ReturnsAsync(testUser);

            var testTasks = new List<TaskModel>();
            if (isAuthenticated && expectedTaskCount > 0)
            {
                for (int i = 1; i <= expectedTaskCount; i++)
                {
                    testTasks.Add(new TaskModel 
                    { 
                        Id = i, 
                        Title = $"Task {i}", 
                        UserId = expectedUserId,
                        Description = $"Description {i}",
                        Deadline = DateTime.Now.AddDays(i),
                        IsCompleted = false,
                        Author = expectedUserName
                    });
                }
            }

            using var context = GetMockDbContext($"IndexTestDb_{isAuthenticated}", testTasks);
            var controller = new TaskController(context, mockUserManager.Object);
            controller.ControllerContext = GetMockControllerContext();

            // Act
            var result = await controller.Index();

            // Assert
            if (isAuthenticated)
            {
                var viewResult = Assert.IsType<ViewResult>(result);
                var model = Assert.IsAssignableFrom<List<TaskModel>>(viewResult.Model);
                Assert.Equal(expectedTaskCount, model.Count);

                if (expectedTaskCount > 0)
                {
                    for (int i = 0; i < expectedTaskCount; i++)
                    {
                        Assert.Equal($"Task {i + 1}", model[i].Title);
                        Assert.Equal($"Description {i + 1}", model[i].Description);
                        Assert.Equal(expectedUserId, model[i].UserId);
                        Assert.False(model[i].IsCompleted);
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

        [Theory]
        [InlineData(true, "1", "test@example.com", "Index", null, null)] // Authenticated user
        [InlineData(false, null, null, "Login", "Account", null)] // Unauthenticated user
        public async Task Create_POST_Task(
            bool isAuthenticated,
            string expectedUserId,
            string expectedAuthor,
            string expectedAction,
            string expectedController,
            string expectedErrorMessage)
        {
            // Arrange
            var mockUserManager = GetMockUserManager();
            var testUser = isAuthenticated ? new User { Id = "1", UserName = "test@example.com" } : null;
            
            mockUserManager.Setup(x => x.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                .ReturnsAsync(testUser);

            using var context = GetMockDbContext($"CreateTestDb_{isAuthenticated}");
            var controller = new TaskController(context, mockUserManager.Object);
            controller.ControllerContext = GetMockControllerContext();

            var newTask = new TaskModel
            {
                Title = "New Test Task",
                Description = "New Test Description",
                Deadline = DateTime.Now.AddDays(1)
            };

            // Act
            var result = await controller.Create(newTask);

            // Assert
            if (isAuthenticated)
            {
                var redirectResult = Assert.IsType<RedirectToActionResult>(result);
                Assert.Equal(expectedAction, redirectResult.ActionName);

                // Verify the task was created with correct user data
                var createdTask = context.Tasks.FirstOrDefault(t => t.Title == "New Test Task");
                Assert.NotNull(createdTask);
                Assert.Equal(expectedUserId, createdTask.UserId);
                Assert.Equal(expectedAuthor, createdTask.Author);
                Assert.False(createdTask.IsCompleted);
            }
            else
            {
                var redirectResult = Assert.IsType<RedirectToActionResult>(result);
                Assert.Equal(expectedAction, redirectResult.ActionName);
                Assert.Equal(expectedController, redirectResult.ControllerName);
            }
        }

        [Fact]
        public void Create_GET_Task()
        {
            // Arrange
            var mockUserManager = GetMockUserManager();
            using var context = GetMockDbContext("CreateGetTestDb");
            var controller = new TaskController(context, mockUserManager.Object);
            controller.ControllerContext = GetMockControllerContext();

            // Act
            var result = controller.Create();

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<TaskModel>(viewResult.Model);
            Assert.Equal(string.Empty, model.Title);
            Assert.Equal(string.Empty, model.Description);
            Assert.Null(model.UserId);
            Assert.Null(model.Author);
            Assert.True(model.Deadline > DateTime.Now);
        }

        [Theory]
        [InlineData(1, true)] // Valid task ID and authenticated user
        [InlineData(999, false)] // Invalid task ID
        [InlineData(1, false, "2")] // Valid task ID but different user
        public async Task Edit_GET_Task(int taskId, bool shouldSucceed, string userId = "1")
        {
            // Arrange
            var mockUserManager = GetMockUserManager();
            var testUser = new User { Id = userId, UserName = "test@example.com" };
            
            mockUserManager.Setup(x => x.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                .ReturnsAsync(testUser);

            var testTasks = new List<TaskModel>
            {
                new TaskModel 
                { 
                    Id = 1, 
                    Title = "Test Task 1", 
                    Description = "Test Description 1",
                    UserId = "1",
                    Author = "test@example.com",
                    Deadline = DateTime.Now.AddDays(1),
                    IsCompleted = false
                }
            };

            using var context = GetMockDbContext($"EditGetTestDb_{taskId}_{userId}", testTasks);
            var controller = new TaskController(context, mockUserManager.Object);
            controller.ControllerContext = GetMockControllerContext(userId);

            // Act
            var result = await controller.Edit(taskId);

            // Assert
            if (shouldSucceed)
            {
                var viewResult = Assert.IsType<ViewResult>(result);
                var model = Assert.IsType<TaskModel>(viewResult.Model);
                Assert.Equal(taskId, model.Id);
                Assert.Equal("Test Task 1", model.Title);
                Assert.Equal("Test Description 1", model.Description);
                Assert.False(model.IsCompleted);
            }
            else
            {
                Assert.IsType<NotFoundResult>(result);
            }
        }

        [Theory]
        [InlineData(1, true)] // Valid task update
        [InlineData(999, false)] // Invalid task ID
        [InlineData(1, false, "2")] // Valid task ID but different user
        public async Task Edit_POST_Task(int taskId, bool shouldSucceed, string userId = "1")
        {
            // Arrange
            var mockUserManager = GetMockUserManager();
            var testUser = new User { Id = userId, UserName = "test@example.com" };
            
            mockUserManager.Setup(x => x.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                .ReturnsAsync(testUser);

            var testTasks = new List<TaskModel>
            {
                new TaskModel 
                { 
                    Id = 1, 
                    Title = "Test Task 1", 
                    Description = "Test Description 1",
                    UserId = "1",
                    Author = "test@example.com",
                    Deadline = DateTime.Now.AddDays(1),
                    IsCompleted = false
                }
            };

            using var context = GetMockDbContext($"EditPostTestDb_{taskId}_{userId}", testTasks);
            var controller = new TaskController(context, mockUserManager.Object);
            controller.ControllerContext = GetMockControllerContext(userId);

            var updatedTask = new TaskModel
            {
                Id = taskId,
                Title = "Updated Title",
                Description = "Updated Description",
                Deadline = DateTime.Now.AddDays(2),
                IsCompleted = true,
                UserId = userId
            };

            // Act
            var result = await controller.Edit(taskId, updatedTask);

            // Assert
            if (shouldSucceed)
            {
                var redirectResult = Assert.IsType<RedirectToActionResult>(result);
                Assert.Equal("Index", redirectResult.ActionName);

                // Verify the task was updated
                var task = await context.Tasks.FindAsync(taskId);
                Assert.NotNull(task);
                Assert.Equal("Updated Title", task.Title);
                Assert.Equal("Updated Description", task.Description);
                Assert.True(task.IsCompleted);
                Assert.Equal(DateTime.Now.AddDays(2).Date, task.Deadline.Date);
            }
            else
            {
                Assert.IsType<NotFoundResult>(result);
            }
        }

        [Theory]
        [InlineData(1, true)] // Valid task deletion
        [InlineData(999, false)] // Invalid task ID
        [InlineData(1, false, "2")] // Valid task ID but different user
        public async Task Delete_POST_Task(int taskId, bool shouldSucceed, string userId = "1")
        {
            // Arrange
            var mockUserManager = GetMockUserManager();
            var testUser = new User { Id = userId, UserName = "test@example.com" };
            
            mockUserManager.Setup(x => x.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                .ReturnsAsync(testUser);

            var testTasks = new List<TaskModel>
            {
                new TaskModel 
                { 
                    Id = 1, 
                    Title = "Test Task 1", 
                    Description = "Test Description 1",
                    UserId = "1",
                    Author = "test@example.com",
                    Deadline = DateTime.Now.AddDays(1),
                    IsCompleted = false
                }
            };

            using var context = GetMockDbContext($"DeleteTestDb_{taskId}_{userId}", testTasks);
            var controller = new TaskController(context, mockUserManager.Object);
            controller.ControllerContext = GetMockControllerContext(userId);

            // Act
            var result = await controller.Delete(taskId);

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Index", redirectResult.ActionName);

            // Verify the task was deleted if it should have been
            var task = await context.Tasks.FindAsync(taskId);
            if (shouldSucceed)
            {
                Assert.Null(task);
            }
            else
            {
                Assert.NotNull(task);
            }
        }

        [Theory]
        [InlineData(1, true)] // Valid task completion
        [InlineData(999, false)] // Invalid task ID
        [InlineData(1, false, "2")] // Valid task ID but different user
        public async Task MarkCompleted_POST_Task(int taskId, bool shouldSucceed, string userId = "1")
        {
            // Arrange
            var mockUserManager = GetMockUserManager();
            var testUser = new User { Id = userId, UserName = "test@example.com" };
            
            mockUserManager.Setup(x => x.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                .ReturnsAsync(testUser);

            var testTasks = new List<TaskModel>
            {
                new TaskModel 
                { 
                    Id = 1, 
                    Title = "Test Task 1", 
                    Description = "Test Description 1",
                    UserId = "1",
                    Author = "test@example.com",
                    Deadline = DateTime.Now.AddDays(1),
                    IsCompleted = false
                }
            };

            using var context = GetMockDbContext($"MarkCompletedTestDb_{taskId}_{userId}", testTasks);
            var controller = new TaskController(context, mockUserManager.Object);
            controller.ControllerContext = GetMockControllerContext(userId);

            // Act
            var result = await controller.MarkCompleted(taskId);

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Index", redirectResult.ActionName);

            // Verify the task completion status
            var task = await context.Tasks.FindAsync(taskId);
            if (shouldSucceed)
            {
                Assert.NotNull(task);
                Assert.True(task.IsCompleted);
            }
            else
            {
                if (task != null)
                {
                    Assert.False(task.IsCompleted);
                }
            }
        }
    }
}
