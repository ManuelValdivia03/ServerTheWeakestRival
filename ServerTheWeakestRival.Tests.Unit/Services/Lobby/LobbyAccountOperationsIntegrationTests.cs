using Microsoft.VisualStudio.TestTools.UnitTesting;
using ServerTheWeakestRival.Tests.Integration.Helpers;
using ServerTheWeakestRival.Tests.Unit.Infrastructure;
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Server.Services;
using ServicesTheWeakestRival.Server.Services.Lobby;
using ServicesTheWeakestRival.Server.Services.Logic;
using System;
using System.Configuration;
using System.ServiceModel;

namespace ServerTheWeakestRival.Tests.Integration.Services.Lobby
{
    [TestClass]
    public sealed class LobbyAccountOperationsIntegrationTests
    {
        private const string TEST_DISPLAY_NAME = "PlayerOne";
        private const string TEST_NEW_DISPLAY_NAME = "PlayerOneRenamed";

        private const string PNG_CONTENT_TYPE = "image/png";

        private const int TOKEN_TTL_MINUTES = 30;

        private string connectionString;
        private LobbyRepository lobbyRepository;
        private LobbyAccountOperations lobbyAccountOperations;

        [TestInitialize]
        public void TestInitialize()
        {
            connectionString = DbTestConfig.GetMainConnectionString();
            DbTestCleaner.CleanupAll();
            TokenStore.Cache.Clear();
            TokenStore.ActiveTokenByUserId.Clear();

            connectionString = ResolveConnectionString(LobbyServiceConstants.MAIN_CONNECTION_STRING_NAME);

            lobbyRepository = new LobbyRepository(connectionString);
            lobbyAccountOperations = new LobbyAccountOperations(
                lobbyRepository,
                () => new UserAvatarSql(connectionString));
        }

        [TestCleanup]
        public void TestCleanup()
        {
            DbTestCleaner.CleanupAll();
            TokenStore.Cache.Clear();
            TokenStore.ActiveTokenByUserId.Clear();
        }

        [TestMethod]
        public void GetMyProfile_WhenValidToken_ReturnsProfile()
        {
            int userId = CreateUser(TEST_DISPLAY_NAME);
            string token = StoreValidTokenForUser(userId);

            UpdateAccountResponse response = lobbyAccountOperations.GetMyProfile(token);

            Assert.IsNotNull(response);
            Assert.AreEqual(userId, response.UserId);
            Assert.IsFalse(string.IsNullOrWhiteSpace(response.Email));
            Assert.AreEqual(TEST_DISPLAY_NAME, response.DisplayName);
            Assert.IsNotNull(response.Avatar);
        }

        [TestMethod]
        public void UpdateAccount_WhenDisplayNameOnly_UpdatesAndReturnsProfile()
        {
            int userId = CreateUser(TEST_DISPLAY_NAME);
            string token = StoreValidTokenForUser(userId);

            var request = new UpdateAccountRequest
            {
                Token = token,
                DisplayName = TEST_NEW_DISPLAY_NAME
            };

            UpdateAccountResponse updated = lobbyAccountOperations.UpdateAccount(request);

            Assert.IsNotNull(updated);
            Assert.AreEqual(userId, updated.UserId);
            Assert.AreEqual(TEST_NEW_DISPLAY_NAME, updated.DisplayName);
        }

        [TestMethod]
        public void UpdateAccount_WhenEmailTaken_ThrowsFaultEmailTaken()
        {
            int userAId = CreateUser("A");
            int userBId = CreateUser("B");

            string tokenB = StoreValidTokenForUser(userBId);

            UpdateAccountResponse profileA = lobbyRepository.GetMyProfile(userAId);
            Assert.IsFalse(string.IsNullOrWhiteSpace(profileA.Email));

            var request = new UpdateAccountRequest
            {
                Token = tokenB,
                Email = profileA.Email
            };

            try
            {
                _ = lobbyAccountOperations.UpdateAccount(request);
                Assert.Fail("Expected FaultException<ServiceFault> was not thrown.");
            }
            catch (FaultException<ServiceFault> ex)
            {
                Assert.IsNotNull(ex.Detail);
                Assert.AreEqual(LobbyServiceConstants.ERROR_EMAIL_TAKEN, ex.Detail.Code);
            }
        }

        [TestMethod]
        public void UpdateAvatar_WhenValid_SavesAvatar()
        {
            int userId = CreateUser(TEST_DISPLAY_NAME);
            string token = StoreValidTokenForUser(userId);

            var request = new UpdateAvatarRequest
            {
                Token = token,
                BodyColor = (byte)AvatarBodyColor.Green,
                PantsColor = (byte)AvatarPantsColor.BlueJeans,
                HatType = (byte)AvatarHatType.Cap,
                HatColor = (byte)AvatarHatColor.Red,
                FaceType = (byte)AvatarFaceType.Happy,
                UseProfilePhotoAsFace = true
            };

            lobbyAccountOperations.UpdateAvatar(request);

            var avatarSql = new UserAvatarSql(connectionString);
            UserAvatarEntity entity = avatarSql.GetByUserId(userId);

            Assert.IsNotNull(entity);
            Assert.AreEqual(userId, entity.UserId);
            Assert.AreEqual(request.BodyColor, entity.BodyColor);
            Assert.AreEqual(request.PantsColor, entity.PantsColor);
            Assert.AreEqual(request.HatType, entity.HatType);
            Assert.AreEqual(request.HatColor, entity.HatColor);
            Assert.AreEqual(request.FaceType, entity.FaceType);
            Assert.AreEqual(request.UseProfilePhotoAsFace, entity.UseProfilePhoto);
        }

        [TestMethod]
        public void UpdateAccount_WhenProfileImageInvalid_ThrowsFaultValidationError()
        {
            int userId = CreateUser(TEST_DISPLAY_NAME);
            string token = StoreValidTokenForUser(userId);

            var request = new UpdateAccountRequest
            {
                Token = token,
                ProfileImageBytes = Array.Empty<byte>(),
                ProfileImageContentType = PNG_CONTENT_TYPE
            };

            try
            {
                _ = lobbyAccountOperations.UpdateAccount(request);
                Assert.Fail("Expected FaultException<ServiceFault> was not thrown.");
            }
            catch (FaultException<ServiceFault> ex)
            {
                Assert.IsNotNull(ex.Detail);
                Assert.AreEqual(LobbyServiceConstants.ERROR_VALIDATION_ERROR, ex.Detail.Code);
            }
        }

        private int CreateUser(string displayName)
        {
            return TestAccountFactory.CreateAccountAndUser(connectionString, displayName);
        }

        private static string ResolveConnectionString(string name)
        {
            ConnectionStringSettings setting = ConfigurationManager.ConnectionStrings[name];

            if (setting == null || string.IsNullOrWhiteSpace(setting.ConnectionString))
            {
                Assert.Fail(string.Format("Missing connectionString '{0}' in test config.", name));
            }

            return setting.ConnectionString;
        }

        private static string StoreValidTokenForUser(int userId)
        {
            if (userId <= 0)
            {
                Assert.Fail("userId must be valid.");
            }

            string tokenValue = Guid.NewGuid().ToString("N");

            TokenStore.StoreToken(
                new AuthToken
                {
                    Token = tokenValue,
                    UserId = userId,
                    ExpiresAtUtc = DateTime.UtcNow.AddMinutes(TOKEN_TTL_MINUTES)
                });

            return tokenValue;
        }
    }
}
