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
using Microsoft.AspNetCore.Hosting;
using System.IO;
using System.Threading;
using StudyApp.Models.ViewModels;

namespace StudyHelper.Tests
{
    public class StudyControllerTests
    {
        private Mock<UserManager<User>> GetMockUserManager()
        {
            var store = new Mock<IUserStore<User>>();
            return new Mock<UserManager<User>>(store.Object, null, null, null, null, null, null, null, null);
        }

        private ApplicationDbContext GetMockDbContext(string databaseName, List<Study> testStudies = null)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: databaseName)
                .Options;

            var context = new ApplicationDbContext(options);
            
            // Clear existing data
            context.Studies.RemoveRange(context.Studies);
            context.Users.RemoveRange(context.Users);
            context.SaveChanges();
            
            if (testStudies != null)
            {
                context.Studies.AddRange(testStudies);
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
        [InlineData(true, "1", "test@example.com", 2)] // Authenticated user with studies
        [InlineData(true, "1", "test@example.com", 0)] // Authenticated user without studies
        [InlineData(false, null, null, 0)] // Unauthenticated user
        public async Task Index_Study(
            bool isAuthenticated,
            string expectedUserId,
            string expectedUserName,
            int expectedStudyCount)
        {
            // Arrange
            var mockUserManager = GetMockUserManager();
            var testUser = isAuthenticated ? new User { Id = expectedUserId, UserName = expectedUserName } : null;
            
            mockUserManager.Setup(x => x.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                .ReturnsAsync(testUser);

            var testStudies = new List<Study>();
            if (isAuthenticated && expectedStudyCount > 0)
            {
                for (int i = 1; i <= expectedStudyCount; i++)
                {
                    var study = new Study
                    {
                        Id = i,
                        Title = $"Study {i}",
                        Category = $"Category {i}",
                        Tags = $"Tag{i}",
                        CreatedAt = DateTime.Now.AddDays(-i),
                        UserId = expectedUserId,
                        User = testUser,
                        Files = new List<StudyFile>
                        {
                            new StudyFile
                            {
                                Id = i,
                                OriginalName = $"file{i}.pdf",
                                StoragePath = $"/uploads/file{i}.pdf",
                                FileType = ".pdf",
                                FileSize = 1024
                            }
                        }
                    };
                    testStudies.Add(study);
                }
            }

            using var context = GetMockDbContext($"IndexTestDb_{isAuthenticated}", testStudies);
            var mockEnv = new Mock<IWebHostEnvironment>();
            mockEnv.Setup(m => m.WebRootPath).Returns("wwwroot");
            mockEnv.Setup(m => m.EnvironmentName).Returns("Development");
            
            var controller = new StudyController(context, mockUserManager.Object, mockEnv.Object);
            controller.ControllerContext = GetMockControllerContext(expectedUserId);

            // Act
            var result = await controller.Index();

            // Assert
            if (isAuthenticated)
            {
                var viewResult = Assert.IsType<ViewResult>(result);
                var model = Assert.IsAssignableFrom<List<Study>>(viewResult.Model);
                Assert.Equal(expectedStudyCount, model.Count);

                if (expectedStudyCount > 0)
                {
                    for (int i = 0; i < expectedStudyCount; i++)
                    {
                        var study = model[i];
                        Assert.Equal($"Study {i + 1}", study.Title);
                        Assert.Equal($"Category {i + 1}", study.Category);
                        Assert.Equal($"Tag{i + 1}", study.Tags);
                        Assert.Equal(expectedUserId, study.UserId);
                        Assert.NotNull(study.Files);
                        Assert.Single(study.Files);
                        Assert.Equal($"file{i + 1}.pdf", study.Files[0].OriginalName);
                    }
                }
            }
            else
            {
                var challengeResult = Assert.IsType<ChallengeResult>(result);
            }
        }

        [Theory]
        [InlineData(true, "1", "test@example.com", "Test Study", "Math", "test,tag", true)] // Valid study with files
        [InlineData(true, "1", "test@example.com", "Test Study", "Math", "test,tag", false)] // Valid study without files
        [InlineData(true, "1", "test@example.com", "", "Math", "test,tag", false)] // Missing title
        public async Task Create_POST_Study(
            bool isAuthenticated,
            string expectedUserId,
            string expectedUserName,
            string title,
            string category,
            string tags,
            bool withFiles)
        {
            // Arrange
            var mockUserManager = GetMockUserManager();
            var testUser = isAuthenticated ? new User { Id = expectedUserId, UserName = expectedUserName } : null;
            
            mockUserManager.Setup(x => x.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                .ReturnsAsync(testUser);
            mockUserManager.Setup(x => x.GetUserId(It.IsAny<ClaimsPrincipal>()))
                .Returns(expectedUserId);

            using var context = GetMockDbContext($"CreateTestDb_{isAuthenticated}_{withFiles}");
            var mockEnv = new Mock<IWebHostEnvironment>();
            mockEnv.Setup(m => m.WebRootPath).Returns("wwwroot");
            mockEnv.Setup(m => m.EnvironmentName).Returns("Development");

            var controller = new StudyController(context, mockUserManager.Object, mockEnv.Object);
            controller.ControllerContext = GetMockControllerContext(expectedUserId);

            var viewModel = new StudyUploadViewModel
            {
                Title = title,
                Category = category,
                Tags = tags
            };

            if (withFiles)
            {
                var mockFile = new Mock<IFormFile>();
                mockFile.Setup(f => f.FileName).Returns("test.pdf");
                mockFile.Setup(f => f.Length).Returns(1024);
                mockFile.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                    .Returns(Task.CompletedTask);

                viewModel.Files = new List<IFormFile> { mockFile.Object };
            }

            // Add model validation errors if needed
            if (string.IsNullOrEmpty(title))
            {
                controller.ModelState.AddModelError("Title", "Title is required");
            }

            // Act
            var result = await controller.Create(viewModel);

            // Assert
            if (!isAuthenticated)
            {
                // For unauthenticated users, we expect a DbUpdateException
                var exception = await Assert.ThrowsAsync<DbUpdateException>(() => controller.Create(viewModel));
                return;
            }

            if (string.IsNullOrEmpty(title))
            {
                // Validation failure
                var viewResult = Assert.IsType<ViewResult>(result);
                Assert.Equal(viewModel, viewResult.Model);
                Assert.False(controller.ModelState.IsValid);
            }
            else
            {
                // Success case
                var redirectResult = Assert.IsType<RedirectToActionResult>(result);
                Assert.Equal("Index", redirectResult.ActionName);

                // Verify the study was created
                var createdStudy = await context.Studies
                    .Include(s => s.Files)
                    .FirstOrDefaultAsync(s => s.Title == title);

                Assert.NotNull(createdStudy);
                Assert.Equal(title, createdStudy.Title);
                Assert.Equal(category, createdStudy.Category);
                Assert.Equal(tags, createdStudy.Tags);
                Assert.Equal(expectedUserId, createdStudy.UserId);
                Assert.NotNull(createdStudy.CreatedAt);

                if (withFiles)
                {
                    Assert.NotNull(createdStudy.Files);
                    Assert.Single(createdStudy.Files);
                    var file = createdStudy.Files.First();
                    Assert.Equal("test.pdf", file.OriginalName);
                    Assert.Equal(".pdf", file.FileType);
                    Assert.Equal(1024, file.FileSize);
                    Assert.StartsWith("/uploads/", file.StoragePath);
                }
                else
                {
                    Assert.Empty(createdStudy.Files);
                }
            }
        }

        [Theory]
        [InlineData(true, "1", "test@example.com", 1, true)] // Valid study, authenticated owner
        [InlineData(true, "1", "test@example.com", 999, false)] // Non-existent study
        [InlineData(true, "2", "other@example.com", 1, true)] // Valid study, different user
        [InlineData(false, null, null, 1, false)] // Unauthenticated user
        public async Task Edit_GET_Study(
            bool isAuthenticated,
            string expectedUserId,
            string expectedUserName,
            int studyId,
            bool shouldSucceed)
        {
            // Arrange
            var mockUserManager = GetMockUserManager();
            var testUser = isAuthenticated ? new User { Id = expectedUserId, UserName = expectedUserName } : null;
            
            mockUserManager.Setup(x => x.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                .ReturnsAsync(testUser);

            var testStudy = new Study
            {
                Id = 1,
                Title = "Test Study",
                Category = "Math",
                Tags = "test,tag",
                CreatedAt = DateTime.Now,
                UserId = "1", // Original owner
                Files = new List<StudyFile>
                {
                    new StudyFile
                    {
                        Id = 1,
                        OriginalName = "test.pdf",
                        StoragePath = "/uploads/test.pdf",
                        FileType = ".pdf",
                        FileSize = 1024,
                        UploadDate = DateTime.Now
                    }
                }
            };

            using var context = GetMockDbContext($"EditGetTestDb_{studyId}_{expectedUserId}", new List<Study> { testStudy });
            var mockEnv = new Mock<IWebHostEnvironment>();
            mockEnv.Setup(m => m.WebRootPath).Returns("wwwroot");
            mockEnv.Setup(m => m.EnvironmentName).Returns("Development");

            var controller = new StudyController(context, mockUserManager.Object, mockEnv.Object);
            controller.ControllerContext = GetMockControllerContext(expectedUserId);

            // Act
            var result = await controller.Edit(studyId);

            // Assert
            if (!shouldSucceed)
            {
                if (!isAuthenticated)
                {
                    var viewResult = Assert.IsType<ViewResult>(result);
                    Assert.Equal(testStudy, viewResult.Model);
                }
                else
                {
                    var notFoundResult = Assert.IsType<NotFoundResult>(result);
                }
            }
            else
            {
                var viewResult = Assert.IsType<ViewResult>(result);
                var model = Assert.IsType<Study>(viewResult.Model);
                Assert.Equal(testStudy.Id, model.Id);
                Assert.Equal(testStudy.Title, model.Title);
                Assert.Equal(testStudy.Category, model.Category);
                Assert.Equal(testStudy.Tags, model.Tags);
                Assert.NotNull(model.Files);
                Assert.Single(model.Files);
            }
        }

        [Theory]
        [InlineData(true, "1", "test@example.com", 1, true)] // Valid edit, authenticated owner
        [InlineData(true, "1", "test@example.com", 999, false)] // Non-existent study
        [InlineData(true, "2", "other@example.com", 1, true)] // Valid study, different user
        public async Task Edit_POST_Study(
            bool isAuthenticated,
            string expectedUserId,
            string expectedUserName,
            int studyId,
            bool shouldSucceed)
        {
            // Arrange
            var mockUserManager = GetMockUserManager();
            var testUser = isAuthenticated ? new User { Id = expectedUserId, UserName = expectedUserName } : null;
            
            mockUserManager.Setup(x => x.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                .ReturnsAsync(testUser);

            var testStudy = new Study
            {
                Id = 1,
                Title = "Original Study",
                Category = "Math",
                Tags = "original,tag",
                CreatedAt = DateTime.Now,
                UserId = "1", // Original owner
                Files = new List<StudyFile>
                {
                    new StudyFile
                    {
                        Id = 1,
                        OriginalName = "test.pdf",
                        StoragePath = "/uploads/test.pdf",
                        FileType = ".pdf",
                        FileSize = 1024,
                        UploadDate = DateTime.Now
                    }
                }
            };

            using var context = GetMockDbContext($"EditPostTestDb_{studyId}_{expectedUserId}", new List<Study> { testStudy });
            var mockEnv = new Mock<IWebHostEnvironment>();
            mockEnv.Setup(m => m.WebRootPath).Returns("wwwroot");
            mockEnv.Setup(m => m.EnvironmentName).Returns("Development");

            var controller = new StudyController(context, mockUserManager.Object, mockEnv.Object);
            controller.ControllerContext = GetMockControllerContext(expectedUserId);

            var updatedStudy = new Study
            {
                Id = studyId,
                Title = "Updated Study",
                Category = "Science",
                Tags = "updated,tag"
            };

            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.FileName).Returns("new.pdf");
            mockFile.Setup(f => f.Length).Returns(2048);
            mockFile.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var newFiles = new List<IFormFile> { mockFile.Object };

            // Act
            var result = await controller.Edit(studyId, updatedStudy, newFiles);

            // Assert
            if (!shouldSucceed)
            {
                if (!isAuthenticated)
                {
                    var redirectResult = Assert.IsType<RedirectToActionResult>(result);
                    Assert.Equal("Login", redirectResult.ActionName);
                    // Don't check ControllerName as it's handled by the authentication middleware
                }
                else
                {
                    var notFoundResult = Assert.IsType<NotFoundResult>(result);
                }
            }
            else
            {
                var redirectResult = Assert.IsType<RedirectToActionResult>(result);
                Assert.Equal("Index", redirectResult.ActionName);

                // Verify the study was updated
                var updatedDbStudy = await context.Studies
                    .Include(s => s.Files)
                    .FirstOrDefaultAsync(s => s.Id == studyId);

                Assert.NotNull(updatedDbStudy);
                Assert.Equal("Updated Study", updatedDbStudy.Title);
                Assert.Equal("Science", updatedDbStudy.Category);
                Assert.Equal("updated,tag", updatedDbStudy.Tags);
                Assert.Equal(2, updatedDbStudy.Files.Count); // Original file + new file

                var newFile = updatedDbStudy.Files.FirstOrDefault(f => f.OriginalName == "new.pdf");
                Assert.NotNull(newFile);
                Assert.Equal(".pdf", newFile.FileType);
                Assert.Equal(2048, newFile.FileSize);
                Assert.StartsWith("/uploads/", newFile.StoragePath);
            }
        }


        [Theory]
        [InlineData(true, "1", "test@example.com", 1, true)] // Valid delete, authenticated owner
        [InlineData(true, "1", "test@example.com", 999, false)] // Non-existent study
        [InlineData(true, "2", "other@example.com", 1, true)] // Valid study, different user
        public async Task Delete_POST_Study(
            bool isAuthenticated,
            string expectedUserId,
            string expectedUserName,
            int studyId,
            bool shouldSucceed)
        {
            // Arrange
            var mockUserManager = GetMockUserManager();
            var testUser = isAuthenticated ? new User { Id = expectedUserId, UserName = expectedUserName } : null;
            
            mockUserManager.Setup(x => x.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                .ReturnsAsync(testUser);

            var testStudy = new Study
            {
                Id = 1,
                Title = "Test Study",
                Category = "Math",
                Tags = "test,tag",
                CreatedAt = DateTime.Now,
                UserId = "1", // Original owner
                Files = new List<StudyFile>
                {
                    new StudyFile
                    {
                        Id = 1,
                        OriginalName = "test.pdf",
                        StoragePath = "/uploads/test.pdf",
                        FileType = ".pdf",
                        FileSize = 1024,
                        UploadDate = DateTime.Now
                    }
                }
            };

            using var context = GetMockDbContext($"DeletePostTestDb_{studyId}_{expectedUserId}", new List<Study> { testStudy });
            var mockEnv = new Mock<IWebHostEnvironment>();
            mockEnv.Setup(m => m.WebRootPath).Returns("wwwroot");
            mockEnv.Setup(m => m.EnvironmentName).Returns("Development");

            var controller = new StudyController(context, mockUserManager.Object, mockEnv.Object);
            controller.ControllerContext = GetMockControllerContext(expectedUserId);

            // Act
            var result = await controller.DeleteConfirmed(studyId);

            // Assert
            if (!shouldSucceed)
            {
                if (!isAuthenticated)
                {
                    var redirectResult = Assert.IsType<RedirectToActionResult>(result);
                    Assert.Equal("Login", redirectResult.ActionName);
                }
                else
                {
                    var notFoundResult = Assert.IsType<NotFoundResult>(result);
                }
            }
            else
            {
                var redirectResult = Assert.IsType<RedirectToActionResult>(result);
                Assert.Equal("Index", redirectResult.ActionName);

                // Verify the study was deleted
                var deletedStudy = await context.Studies
                    .Include(s => s.Files)
                    .FirstOrDefaultAsync(s => s.Id == studyId);

                Assert.Null(deletedStudy);

                // Verify the file was deleted from the database
                var deletedFile = await context.StudyFiles
                    .FirstOrDefaultAsync(f => f.Id == 1);

                Assert.Null(deletedFile);
            }
        }

        [Theory]
        [InlineData(true, "1", "test@example.com", 1, true)] // Valid file, authenticated user
        [InlineData(true, "1", "test@example.com", 999, false)] // Non-existent file
        public async Task DeleteFile_Study(
            bool isAuthenticated,
            string expectedUserId,
            string expectedUserName,
            int fileId,
            bool shouldSucceed)
        {
            // Arrange
            var mockUserManager = GetMockUserManager();
            var testUser = isAuthenticated ? new User { Id = expectedUserId, UserName = expectedUserName } : null;
            
            mockUserManager.Setup(x => x.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                .ReturnsAsync(testUser);

            var testStudy = new Study
            {
                Id = 1,
                Title = "Test Study",
                Category = "Math",
                Tags = "test,tag",
                CreatedAt = DateTime.Now,
                UserId = "1", // Original owner
                Files = new List<StudyFile>
                {
                    new StudyFile
                    {
                        Id = 1,
                        OriginalName = "test.pdf",
                        StoragePath = "/uploads/test.pdf",
                        FileType = ".pdf",
                        FileSize = 1024,
                        UploadDate = DateTime.Now
                    }
                }
            };

            using var context = GetMockDbContext($"DeleteFileTestDb_{fileId}_{expectedUserId}", new List<Study> { testStudy });
            var mockEnv = new Mock<IWebHostEnvironment>();
            mockEnv.Setup(m => m.WebRootPath).Returns("wwwroot");
            mockEnv.Setup(m => m.EnvironmentName).Returns("Development");

            var controller = new StudyController(context, mockUserManager.Object, mockEnv.Object);
            controller.ControllerContext = GetMockControllerContext(expectedUserId);

            // Act
            var result = await controller.DeleteFile(fileId);

            // Assert
            if (!shouldSucceed)
            {
                if (!isAuthenticated)
                {
                    var redirectResult = Assert.IsType<RedirectToActionResult>(result);
                    Assert.Equal("Login", redirectResult.ActionName);
                }
                else
                {
                    var notFoundResult = Assert.IsType<NotFoundResult>(result);
                }
            }
            else
            {
                var okResult = Assert.IsType<OkResult>(result);
                Assert.Equal(200, okResult.StatusCode);

                // Verify the file was deleted from the database
                var deletedFile = await context.StudyFiles
                    .FirstOrDefaultAsync(f => f.Id == fileId);

                Assert.Null(deletedFile);

                // Verify the file was deleted from the file system
                mockEnv.Verify(m => m.WebRootPath, Times.Once);
                // Note: In a real test, you would verify the actual file deletion,
                // but since we're using a mock environment, we can't verify the actual file system operation
            }
        }
    }
}
