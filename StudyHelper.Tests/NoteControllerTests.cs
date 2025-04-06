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
    public class NoteControllerTests
    {
        private Mock<UserManager<User>> GetMockUserManager()
        {
            var store = new Mock<IUserStore<User>>();
            return new Mock<UserManager<User>>(store.Object, null, null, null, null, null, null, null, null);
        }

        private ApplicationDbContext GetMockDbContext(string databaseName)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: databaseName)
                .Options;

            var context = new ApplicationDbContext(options);
            
            // Add test data
            var testNotes = new List<NoteModel>
            {
                new NoteModel 
                { 
                    Id = 1, 
                    Title = "Test Note 1", 
                    Note = "Test Content 1",
                    UserId = "1",
                    Author = "test@example.com",
                    CreatedDate = DateTime.Now
                },
                new NoteModel 
                { 
                    Id = 2, 
                    Title = "Test Note 2", 
                    Note = "Test Content 2",
                    UserId = "1",
                    Author = "test@example.com",
                    CreatedDate = DateTime.Now
                }
            };

            context.Notes.AddRange(testNotes);
            context.SaveChanges();

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

        [Fact]
        public async Task Index_Note()
        {
            // Arrange
            var mockUserManager = GetMockUserManager();
            var testUser = new User { Id = "1", UserName = "test@example.com" };
            
            mockUserManager.Setup(x => x.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                .ReturnsAsync(testUser);

            using var context = GetMockDbContext("IndexTestDb");
            var controller = new NoteController(context, mockUserManager.Object);
            controller.ControllerContext = GetMockControllerContext();

            // Act
            var result = await controller.Index();

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsAssignableFrom<IEnumerable<NoteModel>>(viewResult.Model);
            Assert.Equal(2, model.Count());
        }

        [Theory]
        [InlineData(true, "1", "test@example.com", "Index", null, null)] // Authenticated user
        [InlineData(false, null, null, "Login", "Account", null)] // Unauthenticated user
        public async Task Create_POST_Note(
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
            var controller = new NoteController(context, mockUserManager.Object);
            controller.ControllerContext = GetMockControllerContext();

            var newNote = new NoteModel
            {
                Title = "New Test Note",
                Note = "New Test Content"
            };

            // Act
            var result = await controller.Create(newNote);

            // Assert
            if (isAuthenticated)
            {
                var redirectResult = Assert.IsType<RedirectToActionResult>(result);
                Assert.Equal(expectedAction, redirectResult.ActionName);

                // Verify the note was created with correct user data
                var createdNote = context.Notes.FirstOrDefault(n => n.Title == "New Test Note");
                Assert.NotNull(createdNote);
                Assert.Equal(expectedUserId, createdNote.UserId);
                Assert.Equal(expectedAuthor, createdNote.Author);
                Assert.NotNull(createdNote.CreatedDate);
            }
            else
            {
                var redirectResult = Assert.IsType<RedirectToActionResult>(result);
                Assert.Equal(expectedAction, redirectResult.ActionName);
                Assert.Equal(expectedController, redirectResult.ControllerName);
            }
        }

        [Fact]
        public void Create_GET_Note()
        {
            // Arrange
            var mockUserManager = GetMockUserManager();
            using var context = GetMockDbContext("CreateGetTestDb");
            var controller = new NoteController(context, mockUserManager.Object);
            controller.ControllerContext = GetMockControllerContext();

            // Act
            var result = controller.Create();

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<NoteModel>(viewResult.Model);
            Assert.Equal(string.Empty, model.Title);
            Assert.Equal(string.Empty, model.Note);
            Assert.Null(model.UserId);
            Assert.Null(model.Author);
        }

        [Theory]
        [InlineData(1, true)] // Valid note ID and authenticated user
        [InlineData(999, false)] // Invalid note ID
        [InlineData(1, false, "2")] // Valid note ID but different user
        public async Task Edit_GET_Note(int noteId, bool shouldSucceed, string userId = "1")
        {
            // Arrange
            var mockUserManager = GetMockUserManager();
            var testUser = new User { Id = userId, UserName = "test@example.com" };
            
            mockUserManager.Setup(x => x.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                .ReturnsAsync(testUser);

            using var context = GetMockDbContext($"EditGetTestDb_{noteId}_{userId}");
            var controller = new NoteController(context, mockUserManager.Object);
            controller.ControllerContext = GetMockControllerContext(userId);

            // Act
            var result = await controller.Edit(noteId);

            // Assert
            if (shouldSucceed)
            {
                var viewResult = Assert.IsType<ViewResult>(result);
                var model = Assert.IsType<NoteModel>(viewResult.Model);
                Assert.Equal(noteId, model.Id);
                Assert.Equal("Test Note 1", model.Title);
                Assert.Equal("Test Content 1", model.Note);
            }
            else
            {
                Assert.IsType<NotFoundResult>(result);
            }
        }

        [Theory]
        [InlineData(1, true)] // Valid note update
        [InlineData(999, false)] // Invalid note ID
        [InlineData(1, false, "2")] // Valid note ID but different user
        public async Task Edit_POST_Note(int noteId, bool shouldSucceed, string userId = "1")
        {
            // Arrange
            var mockUserManager = GetMockUserManager();
            var testUser = new User { Id = userId, UserName = "test@example.com" };
            
            mockUserManager.Setup(x => x.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                .ReturnsAsync(testUser);

            using var context = GetMockDbContext($"EditPostTestDb_{noteId}_{userId}");
            var controller = new NoteController(context, mockUserManager.Object);
            controller.ControllerContext = GetMockControllerContext(userId);

            var updatedNote = new NoteModel
            {
                Id = noteId,
                Title = "Updated Title",
                Note = "Updated Content",
                UserId = userId
            };

            // Act
            var result = await controller.Edit(noteId, updatedNote);

            // Assert
            if (shouldSucceed)
            {
                var redirectResult = Assert.IsType<RedirectToActionResult>(result);
                Assert.Equal("Index", redirectResult.ActionName);

                // Verify the note was updated
                var note = await context.Notes.FindAsync(noteId);
                Assert.NotNull(note);
                Assert.Equal("Updated Title", note.Title);
                Assert.Equal("Updated Content", note.Note);
                Assert.NotNull(note.LastModifiedDate);
            }
            else
            {
                // For both invalid ID and unauthorized user, we expect NotFound
                Assert.IsType<NotFoundResult>(result);
            }
        }

        [Theory]
        [InlineData(1, true)] // Valid note deletion
        [InlineData(999, false)] // Invalid note ID
        [InlineData(1, false, "2")] // Valid note ID but different user
        public async Task Delete_POST_Note(int noteId, bool shouldSucceed, string userId = "1")
        {
            // Arrange
            var mockUserManager = GetMockUserManager();
            var testUser = new User { Id = userId, UserName = "test@example.com" };
            
            mockUserManager.Setup(x => x.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                .ReturnsAsync(testUser);

            using var context = GetMockDbContext($"DeleteTestDb_{noteId}_{userId}");
            var controller = new NoteController(context, mockUserManager.Object);
            controller.ControllerContext = GetMockControllerContext(userId);

            // Act
            var result = await controller.Delete(noteId);

            // Assert
            if (shouldSucceed)
            {
                var redirectResult = Assert.IsType<RedirectToActionResult>(result);
                Assert.Equal("Index", redirectResult.ActionName);

                // Verify the note was deleted
                var note = await context.Notes.FindAsync(noteId);
                Assert.Null(note);
            }
            else
            {
                // For both invalid ID and unauthorized user, we expect NotFound
                Assert.IsType<NotFoundResult>(result);
            }
        }
    }
}
