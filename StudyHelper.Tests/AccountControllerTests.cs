using Xunit;
using Moq;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StudyApp.Models;
using StudyApp.Data;
using System.Collections.Generic;
using System.Linq;
using System;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using StudyApp.Models.ViewModels;

namespace StudyHelper.Tests
{
    // Test class for UserManager<User>
    public class TestUserManager : UserManager<User>
    {
        private readonly IQueryable<User> _testUsers;
        private readonly List<User> _users = new List<User>();

        public TestUserManager(IQueryable<User> testUsers)
            : base(new Mock<IUserStore<User>>().Object,
                   new Mock<IOptions<IdentityOptions>>().Object,
                   new Mock<IPasswordHasher<User>>().Object,
                   new List<IUserValidator<User>>(),
                   new List<IPasswordValidator<User>>(),
                   new Mock<ILookupNormalizer>().Object,
                   new IdentityErrorDescriber(),
                   new Mock<IServiceProvider>().Object,
                   new Mock<ILogger<UserManager<User>>>().Object)
        {
            _testUsers = testUsers;
            _users.AddRange(testUsers);
        }

        public override IQueryable<User> Users => _testUsers;

        public override Task<IdentityResult> CreateAsync(User user, string password)
        {
            _users.Add(user);
            return Task.FromResult(IdentityResult.Success);
        }

        public override Task<User> FindByEmailAsync(string email)
        {
            return Task.FromResult(_users.FirstOrDefault(u => u.Email == email));
        }
    }

    // Unit test class
    public class AccountControllerTests
    {
        private SignInManager<User> GetMockSignInManager(UserManager<User> userManager, bool shouldSucceed = true)
        {
            var mockSignInManager = new Mock<SignInManager<User>>(
                userManager,
                new Mock<IHttpContextAccessor>().Object,
                new Mock<IUserClaimsPrincipalFactory<User>>().Object,
                null, null, null, null);

            mockSignInManager.Setup(x => x.PasswordSignInAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>()))
                .ReturnsAsync(shouldSucceed ? Microsoft.AspNetCore.Identity.SignInResult.Success : Microsoft.AspNetCore.Identity.SignInResult.Failed);

            mockSignInManager.Setup(x => x.SignInAsync(It.IsAny<User>(), It.IsAny<bool>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            mockSignInManager.Setup(x => x.SignOutAsync())
                .Returns(Task.CompletedTask);

            return mockSignInManager.Object;
        }

        private ApplicationDbContext GetMockDbContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: "TestDb")
                .Options;

            return new Mock<ApplicationDbContext>(options).Object;
        }

        [Fact]
        public void GetUsers_Returns_List_Of_Users_With_Real_UserManager()
        {
            // Arrange
            var testUsers = new List<User>
            {
                new User { Id = "1", UserName = "alice", Email = "alice@mail.com", FullName = "Alice A" },
                new User { Id = "2", UserName = "bob", Email = "bob@mail.com", FullName = "Bob B" }
            }.AsQueryable();

            var realUserManager = new TestUserManager(testUsers);
            var mockSignInManager = GetMockSignInManager(realUserManager);
            var mockDbContext = GetMockDbContext();

            var controller = new AccountController(realUserManager, mockSignInManager, mockDbContext);

            // Act
            var result = controller.GetUsers();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var users = Assert.IsAssignableFrom<IEnumerable<object>>(okResult.Value);
            Assert.Equal(2, users.Count());
        }

        [Fact]
        public void Register_GET_Returns_View()
        {
            // Arrange
            var testUsers = new List<User>().AsQueryable();
            var realUserManager = new TestUserManager(testUsers);
            var mockSignInManager = GetMockSignInManager(realUserManager);
            var mockDbContext = GetMockDbContext();

            var controller = new AccountController(realUserManager, mockSignInManager, mockDbContext);

            // Act
            var result = controller.Register();

            // Assert
            Assert.IsType<ViewResult>(result);
        }

        [Fact]
        public async Task Register_POST_With_Valid_Model_Creates_User_And_Redirects()
        {
            // Arrange
            var testUsers = new List<User>().AsQueryable();
            var realUserManager = new TestUserManager(testUsers);
            var mockSignInManager = GetMockSignInManager(realUserManager);
            var mockDbContext = GetMockDbContext();

            var controller = new AccountController(realUserManager, mockSignInManager, mockDbContext);
            var model = new RegisterViewModel
            {
                FullName = "Test User",
                Email = "test@example.com",
                Password = "Test123!",
                ConfirmPassword = "Test123!"
            };

            // Act
            var result = await controller.Register(model);

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Index", redirectResult.ActionName);
            Assert.Equal("Home", redirectResult.ControllerName);
        }

        [Fact]
        public async Task Register_POST_With_Invalid_Model_Returns_View()
        {
            // Arrange
            var testUsers = new List<User>().AsQueryable();
            var realUserManager = new TestUserManager(testUsers);
            var mockSignInManager = GetMockSignInManager(realUserManager);
            var mockDbContext = GetMockDbContext();

            var controller = new AccountController(realUserManager, mockSignInManager, mockDbContext);
            var model = new RegisterViewModel
            {
                FullName = "", // Invalid - required field
                Email = "invalid-email", // Invalid email format
                Password = "123", // Too short
                ConfirmPassword = "456" // Doesn't match
            };

            controller.ModelState.AddModelError("", "Test error");

            // Act
            var result = await controller.Register(model);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Equal(model, viewResult.Model);
        }

        [Fact]
        public void Login_GET_Returns_View()
        {
            // Arrange
            var testUsers = new List<User>().AsQueryable();
            var realUserManager = new TestUserManager(testUsers);
            var mockSignInManager = GetMockSignInManager(realUserManager);
            var mockDbContext = GetMockDbContext();

            var controller = new AccountController(realUserManager, mockSignInManager, mockDbContext);

            // Act
            var result = controller.Login();

            // Assert
            Assert.IsType<ViewResult>(result);
        }

        [Fact]
        public async Task Login_POST_With_Valid_Credentials_Redirects_To_Home()
        {
            // Arrange
            var testUsers = new List<User>
            {
                new User { Id = "1", UserName = "test@example.com", Email = "test@example.com", FullName = "Test User" }
            }.AsQueryable();
            
            var realUserManager = new TestUserManager(testUsers);
            var mockSignInManager = GetMockSignInManager(realUserManager, shouldSucceed: true);
            var mockDbContext = GetMockDbContext();

            var controller = new AccountController(realUserManager, mockSignInManager, mockDbContext);
            var model = new LoginViewModel
            {
                Email = "test@example.com",
                Password = "Test123!",
                RememberMe = false
            };

            // Act
            var result = await controller.Login(model);

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Index", redirectResult.ActionName);
            Assert.Equal("Home", redirectResult.ControllerName);
        }

        [Fact]
        public async Task Login_POST_With_Invalid_Credentials_Returns_View_With_Error()
        {
            // Arrange
            var testUsers = new List<User>().AsQueryable();
            var realUserManager = new TestUserManager(testUsers);
            var mockSignInManager = GetMockSignInManager(realUserManager, shouldSucceed: false);
            var mockDbContext = GetMockDbContext();

            var controller = new AccountController(realUserManager, mockSignInManager, mockDbContext);
            var model = new LoginViewModel
            {
                Email = "nonexistent@example.com",
                Password = "WrongPassword",
                RememberMe = false
            };

            // Act
            var result = await controller.Login(model);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Equal(model, viewResult.Model);
            Assert.True(controller.ModelState.ErrorCount > 0);
        }

        [Fact]
        public async Task Logout_Redirects_To_Login()
        {
            // Arrange
            var testUsers = new List<User>().AsQueryable();
            var realUserManager = new TestUserManager(testUsers);
            var mockSignInManager = GetMockSignInManager(realUserManager);
            var mockDbContext = GetMockDbContext();

            var controller = new AccountController(realUserManager, mockSignInManager, mockDbContext);

            // Act
            var result = await controller.Logout();

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Login", redirectResult.ActionName);
        }
    }
}
