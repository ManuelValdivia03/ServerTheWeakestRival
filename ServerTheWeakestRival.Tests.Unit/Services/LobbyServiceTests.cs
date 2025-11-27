using System;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Server.Services;

namespace ServerTheWeakestRival.Tests.Unit.Services
{
    [TestClass]
    public sealed class LobbyServiceTests
    {
        private const string ERROR_INVALID_REQUEST = "INVALID_REQUEST";
        private const string ERROR_VALIDATION_ERROR = "VALIDATION_ERROR";
        private const string ERROR_UNAUTHORIZED = "UNAUTHORIZED";
        private const string ERROR_EMAIL_TAKEN = "EMAIL_TAKEN";

        private const int MAX_DISPLAY_NAME_LENGTH = 80;
        private const int MAX_PROFILE_IMAGE_URL_LENGTH = 500;
        private const int MAX_EMAIL_LENGTH = 320;

        private static readonly PrivateType LobbyServicePrivateType = new PrivateType(typeof(LobbyService));

        private LobbyService service;

        [TestInitialize]
        public void SetUp()
        {
            service = new LobbyService();
        }

        private static ServiceFault AssertIsServiceFault(Exception ex, string expectedCode)
        {
            Assert.IsNotNull(ex);

            Exception actual = ex;

            if (ex is TargetInvocationException tie && tie.InnerException != null)
            {
                actual = tie.InnerException;
            }

            var detailProperty = actual.GetType().GetProperty("Detail");
            Assert.IsNotNull(detailProperty, "Exception does not have Detail property.");

            var detail = detailProperty.GetValue(actual) as ServiceFault;
            Assert.IsNotNull(detail, "Detail is not a ServiceFault.");

            Assert.AreEqual(expectedCode, detail.Code);

            return detail;
        }

        #region ListLobbies

        [TestMethod]
        public void ListLobbies_NullRequest_ReturnsEmptyList()
        {
            ListLobbiesResponse response = service.ListLobbies(null);

            Assert.IsNotNull(response);
            Assert.IsNotNull(response.Lobbies);
            Assert.AreEqual(0, response.Lobbies.Count);
        }

        [TestMethod]
        public void ListLobbies_WithRequest_ReturnsEmptyList()
        {
            var request = new ListLobbiesRequest();

            ListLobbiesResponse response = service.ListLobbies(request);

            Assert.IsNotNull(response.Lobbies);
            Assert.AreEqual(0, response.Lobbies.Count);
        }

        [TestMethod]
        public void ListLobbies_MultipleCalls_ReturnNewInstances()
        {
            ListLobbiesResponse first = service.ListLobbies(null);
            ListLobbiesResponse second = service.ListLobbies(null);

            Assert.AreNotSame(first, second);
            Assert.AreNotSame(first.Lobbies, second.Lobbies);
        }

        [TestMethod]
        public void ListLobbies_LobbiesListIsAlwaysNonNull()
        {
            ListLobbiesResponse response = service.ListLobbies(new ListLobbiesRequest());

            Assert.IsNotNull(response.Lobbies);
        }

        [TestMethod]
        public void ListLobbies_LobbiesListStartsEmpty()
        {
            ListLobbiesResponse response = service.ListLobbies(new ListLobbiesRequest());

            Assert.AreEqual(0, response.Lobbies.Count);
        }

        #endregion

        #region LeaveLobby

        [TestMethod]
        public void LeaveLobby_NullRequest_DoesNotThrow()
        {
            service.LeaveLobby(null);
        }

        [TestMethod]
        public void LeaveLobby_EmptyLobbyId_DoesNotThrow()
        {
            var request = new LeaveLobbyRequest
            {
                LobbyId = Guid.Empty,
                Token = "token"
            };

            service.LeaveLobby(request);
        }

        [TestMethod]
        public void LeaveLobby_NullToken_DoesNotThrow()
        {
            var request = new LeaveLobbyRequest
            {
                LobbyId = Guid.NewGuid(),
                Token = null
            };

            service.LeaveLobby(request);
        }

        [TestMethod]
        public void LeaveLobby_EmptyToken_DoesNotThrow()
        {
            var request = new LeaveLobbyRequest
            {
                LobbyId = Guid.NewGuid(),
                Token = string.Empty
            };

            service.LeaveLobby(request);
        }

        [TestMethod]
        public void LeaveLobby_WhitespaceToken_DoesNotThrow()
        {
            var request = new LeaveLobbyRequest
            {
                LobbyId = Guid.NewGuid(),
                Token = "  "
            };

            service.LeaveLobby(request);
        }

        #endregion

        #region SendChatMessage

        [TestMethod]
        public void SendChatMessage_NullRequest_DoesNotThrow()
        {
            service.SendChatMessage(null);
        }

        [TestMethod]
        public void SendChatMessage_EmptyLobbyId_DoesNotThrow()
        {
            var request = new SendLobbyMessageRequest
            {
                LobbyId = Guid.Empty,
                Token = "token",
                Message = "Test"
            };

            service.SendChatMessage(request);
        }

        [TestMethod]
        public void SendChatMessage_NullToken_DoesNotThrow()
        {
            var request = new SendLobbyMessageRequest
            {
                LobbyId = Guid.NewGuid(),
                Token = null,
                Message = "Test"
            };

            service.SendChatMessage(request);
        }

        [TestMethod]
        public void SendChatMessage_EmptyToken_DoesNotThrow()
        {
            var request = new SendLobbyMessageRequest
            {
                LobbyId = Guid.NewGuid(),
                Token = string.Empty,
                Message = "Test"
            };

            service.SendChatMessage(request);
        }

        [TestMethod]
        public void SendChatMessage_WhitespaceToken_DoesNotThrow()
        {
            var request = new SendLobbyMessageRequest
            {
                LobbyId = Guid.NewGuid(),
                Token = "   ",
                Message = "Test"
            };

            service.SendChatMessage(request);
        }

        #endregion

        #region UpdateAccount / Validations

        [TestMethod]
        public void UpdateAccount_NullRequest_ThrowsInvalidRequestFault()
        {
            try
            {
                service.UpdateAccount(null);
                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_INVALID_REQUEST);
                Assert.AreEqual("Request nulo.", detail.Message);
            }
        }

        [TestMethod]
        public void ValidateProfileChanges_DisplayNameTooLong_ThrowsFault()
        {
            var request = new UpdateAccountRequest
            {
                DisplayName = new string('x', MAX_DISPLAY_NAME_LENGTH + 1),
                ProfileImageUrl = null
            };

            const bool hasDisplayNameChange = true;
            const bool hasProfileImageChange = false;

            try
            {
                LobbyServicePrivateType.InvokeStatic(
                    "ValidateProfileChanges",
                    request,
                    hasDisplayNameChange,
                    hasProfileImageChange);

                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                AssertIsServiceFault(ex, ERROR_VALIDATION_ERROR);
            }
        }

        [TestMethod]
        public void ValidateProfileChanges_ProfileImageTooLong_ThrowsFault()
        {
            var request = new UpdateAccountRequest
            {
                DisplayName = "Name",
                ProfileImageUrl = new string('y', MAX_PROFILE_IMAGE_URL_LENGTH + 1)
            };

            const bool hasDisplayNameChange = false;
            const bool hasProfileImageChange = true;

            try
            {
                LobbyServicePrivateType.InvokeStatic(
                    "ValidateProfileChanges",
                    request,
                    hasDisplayNameChange,
                    hasProfileImageChange);

                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                AssertIsServiceFault(ex, ERROR_VALIDATION_ERROR);
            }
        }

        [TestMethod]
        public void ValidateProfileChanges_WithValidLengths_DoesNotThrow()
        {
            var request = new UpdateAccountRequest
            {
                DisplayName = "Name",
                ProfileImageUrl = "http://image.com/avatar.png"
            };

            const bool hasDisplayNameChange = true;
            const bool hasProfileImageChange = true;

            LobbyServicePrivateType.InvokeStatic(
                "ValidateProfileChanges",
                request,
                hasDisplayNameChange,
                hasProfileImageChange);
        }

        [TestMethod]
        public void ValidateProfileChanges_NoChangeFlags_DoesNotThrow()
        {
            var request = new UpdateAccountRequest
            {
                DisplayName = new string('x', MAX_DISPLAY_NAME_LENGTH + 1),
                ProfileImageUrl = new string('y', MAX_PROFILE_IMAGE_URL_LENGTH + 1)
            };

            const bool hasDisplayNameChange = false;
            const bool hasProfileImageChange = false;

            LobbyServicePrivateType.InvokeStatic(
                "ValidateProfileChanges",
                request,
                hasDisplayNameChange,
                hasProfileImageChange);
        }

        [TestMethod]
        public void ValidateAndNormalizeEmail_Valid_ReturnsTrimmed()
        {
            const string raw = "  user@example.com  ";

            string result = (string)LobbyServicePrivateType.InvokeStatic(
                "ValidateAndNormalizeEmail",
                raw);

            Assert.AreEqual("user@example.com", result);
        }

        [TestMethod]
        public void ValidateAndNormalizeEmail_Invalid_ThrowsFault()
        {
            const string raw = "not-email";

            try
            {
                LobbyServicePrivateType.InvokeStatic(
                    "ValidateAndNormalizeEmail",
                    raw);

                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_VALIDATION_ERROR);
                Assert.AreEqual("Email inválido.", detail.Message);
            }
        }

        [TestMethod]
        public void ValidateAndNormalizeEmail_TooLong_ThrowsFault()
        {
            string localPart = new string('a', MAX_EMAIL_LENGTH);
            string email = localPart + "@example.com";

            try
            {
                LobbyServicePrivateType.InvokeStatic(
                    "ValidateAndNormalizeEmail",
                    email);

                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_VALIDATION_ERROR);
                Assert.AreEqual($"Email máximo {MAX_EMAIL_LENGTH}.", detail.Message);
            }
        }

        [TestMethod]
        public void ValidateAndNormalizeEmail_Empty_ThrowsFault()
        {
            const string raw = "";

            try
            {
                LobbyServicePrivateType.InvokeStatic(
                    "ValidateAndNormalizeEmail",
                    raw);

                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                AssertIsServiceFault(ex, ERROR_VALIDATION_ERROR);
            }
        }

        [TestMethod]
        public void ValidateAndNormalizeEmail_Whitespace_ThrowsFault()
        {
            const string raw = "   ";

            try
            {
                LobbyServicePrivateType.InvokeStatic(
                    "ValidateAndNormalizeEmail",
                    raw);

                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                AssertIsServiceFault(ex, ERROR_VALIDATION_ERROR);
            }
        }

        #endregion

        #region JoinByCode

        [TestMethod]
        public void JoinByCode_NullRequest_ThrowsInvalidRequest()
        {
            try
            {
                service.JoinByCode(null);
                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_INVALID_REQUEST);
                Assert.AreEqual("Request nulo.", detail.Message);
            }
        }

        [TestMethod]
        public void JoinByCode_EmptyAccessCode_ThrowsInvalidRequest()
        {
            var request = new JoinByCodeRequest
            {
                Token = "token",
                AccessCode = "   "
            };

            try
            {
                service.JoinByCode(request);
                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_INVALID_REQUEST);
                Assert.AreEqual("AccessCode requerido.", detail.Message);
            }
        }

        [TestMethod]
        public void JoinByCode_NullAccessCode_ThrowsInvalidRequest()
        {
            var request = new JoinByCodeRequest
            {
                Token = "token",
                AccessCode = null
            };

            try
            {
                service.JoinByCode(request);
                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_INVALID_REQUEST);
                Assert.AreEqual("AccessCode requerido.", detail.Message);
            }
        }

        [TestMethod]
        public void JoinByCode_NullToken_ThrowsUnauthorized()
        {
            var request = new JoinByCodeRequest
            {
                Token = null,
                AccessCode = "ABC123"
            };

            try
            {
                service.JoinByCode(request);
                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                AssertIsServiceFault(ex, ERROR_UNAUTHORIZED);
            }
        }

        [TestMethod]
        public void JoinByCode_EmptyToken_ThrowsUnauthorized()
        {
            var request = new JoinByCodeRequest
            {
                Token = string.Empty,
                AccessCode = "ABC123"
            };

            try
            {
                service.JoinByCode(request);
                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                AssertIsServiceFault(ex, ERROR_UNAUTHORIZED);
            }
        }

        #endregion

        #region StartLobbyMatch

        [TestMethod]
        public void StartLobbyMatch_NullRequest_ThrowsInvalidRequest()
        {
            try
            {
                service.StartLobbyMatch(null);
                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_INVALID_REQUEST);
                Assert.AreEqual("Request nulo.", detail.Message);
            }
        }

        [TestMethod]
        public void StartLobbyMatch_NullToken_ThrowsUnauthorized()
        {
            var request = new StartLobbyMatchRequest
            {
                Token = null
            };

            try
            {
                service.StartLobbyMatch(request);
                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                AssertIsServiceFault(ex, ERROR_UNAUTHORIZED);
            }
        }

        [TestMethod]
        public void StartLobbyMatch_EmptyToken_ThrowsUnauthorized()
        {
            var request = new StartLobbyMatchRequest
            {
                Token = string.Empty
            };

            try
            {
                service.StartLobbyMatch(request);
                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                AssertIsServiceFault(ex, ERROR_UNAUTHORIZED);
            }
        }

        [TestMethod]
        public void StartLobbyMatch_MaxPlayersZero_WhenUnauthorizedStillThrowsFault()
        {
            var request = new StartLobbyMatchRequest
            {
                Token = string.Empty,
                MaxPlayers = 0
            };

            try
            {
                service.StartLobbyMatch(request);
                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_UNAUTHORIZED);
                Assert.IsFalse(string.IsNullOrWhiteSpace(detail.Message));
            }
        }

        [TestMethod]
        public void StartLobbyMatch_NullConfig_WhenUnauthorizedStillThrowsFault()
        {
            var request = new StartLobbyMatchRequest
            {
                Token = string.Empty,
                Config = null
            };

            try
            {
                service.StartLobbyMatch(request);
                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_UNAUTHORIZED);
                Assert.IsFalse(string.IsNullOrWhiteSpace(detail.Message));
            }
        }

        #endregion

        #region UpdateAvatar

        [TestMethod]
        public void UpdateAvatar_NullRequest_ThrowsInvalidRequest()
        {
            try
            {
                service.UpdateAvatar(null);
                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                ServiceFault detail = AssertIsServiceFault(ex, ERROR_INVALID_REQUEST);
                Assert.AreEqual("Request nulo.", detail.Message);
            }
        }

        [TestMethod]
        public void UpdateAvatar_NullToken_ThrowsUnauthorized()
        {
            var request = new UpdateAvatarRequest
            {
                Token = null
            };

            try
            {
                service.UpdateAvatar(request);
                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                AssertIsServiceFault(ex, ERROR_UNAUTHORIZED);
            }
        }

        [TestMethod]
        public void UpdateAvatar_EmptyToken_ThrowsUnauthorized()
        {
            var request = new UpdateAvatarRequest
            {
                Token = string.Empty
            };

            try
            {
                service.UpdateAvatar(request);
                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                AssertIsServiceFault(ex, ERROR_UNAUTHORIZED);
            }
        }

        [TestMethod]
        public void UpdateAvatar_WhitespaceToken_ThrowsUnauthorized()
        {
            var request = new UpdateAvatarRequest
            {
                Token = "   "
            };

            try
            {
                service.UpdateAvatar(request);
                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                AssertIsServiceFault(ex, ERROR_UNAUTHORIZED);
            }
        }

        [TestMethod]
        public void UpdateAvatar_WithValuesButInvalidToken_ThrowsUnauthorized()
        {
            var request = new UpdateAvatarRequest
            {
                Token = "invalid",
                BodyColor = (byte)AvatarBodyColor.Green,
                PantsColor = (byte)AvatarPantsColor.BlueJeans,
                HatType = (byte)AvatarHatType.Cap,
                HatColor = (byte)AvatarHatColor.Black,
                FaceType = (byte)AvatarFaceType.Happy,
                UseProfilePhotoAsFace = true
            };

            try
            {
                service.UpdateAvatar(request);
                Assert.Fail("Expected fault was not thrown.");
            }
            catch (Exception ex)
            {
                AssertIsServiceFault(ex, ERROR_UNAUTHORIZED);
            }
        }

        #endregion

        #region Helpers

        [TestMethod]
        public void MapAvatar_NullEntity_ReturnsDefault()
        {
            var result = (AvatarAppearanceDto)LobbyServicePrivateType.InvokeStatic(
                "MapAvatar",
                new object[] { null });

            Assert.AreEqual(AvatarBodyColor.Blue, result.BodyColor);
            Assert.AreEqual(AvatarPantsColor.Black, result.PantsColor);
            Assert.AreEqual(AvatarHatType.None, result.HatType);
            Assert.AreEqual(AvatarHatColor.Default, result.HatColor);
            Assert.AreEqual(AvatarFaceType.Default, result.FaceType);
            Assert.IsFalse(result.UseProfilePhotoAsFace);
        }

        [TestMethod]
        public void MapAvatar_ValidEntity_MapsValues()
        {
            var entity = new UserAvatarEntity
            {
                UserId = 1,
                BodyColor = (byte)AvatarBodyColor.Green,
                PantsColor = (byte)AvatarPantsColor.BlueJeans,
                HatType = (byte)AvatarHatType.Cap,
                HatColor = (byte)AvatarHatColor.Black,
                FaceType = (byte)AvatarFaceType.Happy,
                UseProfilePhoto = true
            };

            var result = (AvatarAppearanceDto)LobbyServicePrivateType.InvokeStatic(
                "MapAvatar",
                new object[] { entity });

            Assert.AreEqual(AvatarBodyColor.Green, result.BodyColor);
            Assert.AreEqual(AvatarPantsColor.BlueJeans, result.PantsColor);
            Assert.AreEqual(AvatarHatType.Cap, result.HatType);
            Assert.AreEqual(AvatarHatColor.Black, result.HatColor);
            Assert.AreEqual(AvatarFaceType.Happy, result.FaceType);
            Assert.IsTrue(result.UseProfilePhotoAsFace);
        }

        [TestMethod]
        public void IsValidEmail_Valid_ReturnsTrue()
        {
            const string email = "user@example.com";

            bool result = (bool)LobbyServicePrivateType.InvokeStatic(
                "IsValidEmail",
                email);

            Assert.IsTrue(result);
        }

        [TestMethod]
        public void IsValidEmail_Invalid_ReturnsFalse()
        {
            const string email = "invalid";

            bool result = (bool)LobbyServicePrivateType.InvokeStatic(
                "IsValidEmail",
                email);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void IsValidEmail_Empty_ReturnsFalse()
        {
            const string email = "";

            bool result = (bool)LobbyServicePrivateType.InvokeStatic(
                "IsValidEmail",
                email);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void IsValidEmail_Whitespace_ReturnsFalse()
        {
            const string email = "   ";

            bool result = (bool)LobbyServicePrivateType.InvokeStatic(
                "IsValidEmail",
                email);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void IsValidEmail_WithPlusSign_ReturnsTrue()
        {
            const string email = "user+test@example.com";

            bool result = (bool)LobbyServicePrivateType.InvokeStatic(
                "IsValidEmail",
                email);

            Assert.IsTrue(result);
        }

        [TestMethod]
        public void ThrowFault_BuildsFaultWithCodeAndMessage()
        {
            const string code = ERROR_EMAIL_TAKEN;
            const string message = "Ese email ya está en uso.";

            var ex = (Exception)LobbyServicePrivateType.InvokeStatic(
                "ThrowFault",
                code,
                message);

            ServiceFault detail = AssertIsServiceFault(ex, ERROR_EMAIL_TAKEN);
            Assert.AreEqual(message, detail.Message);
        }

        [TestMethod]
        public void TryGetLobbyUidForCurrentSession_NoCallbacks_ReturnsFalseAndEmptyGuid()
        {
            object[] parameters = { Guid.Empty };

            bool result = (bool)LobbyServicePrivateType.InvokeStatic(
                "TryGetLobbyUidForCurrentSession",
                parameters);

            var lobbyUid = (Guid)parameters[0];

            Assert.IsFalse(result);
            Assert.AreEqual(Guid.Empty, lobbyUid);
        }

        #endregion
    }
}
