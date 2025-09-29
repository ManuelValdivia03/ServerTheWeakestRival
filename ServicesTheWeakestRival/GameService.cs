using System;
using System.Linq;
using TheWeakestRival.Data;

namespace ServicesTheWeakestRival
{
    public sealed class GameService : IGameService
    {
        public string Ping(string message)
        {
            try
            {
                using (var db = new TheWeakestRivalEntities())
                {
                    var count = db.Accounts.Count();
                    return $"pong: {message} (Te escucho)";
                }
            }
            catch (Exception ex)
            {
                return $"pong: {message} (DB ERROR: {ex.Message})";
            }
        }

        public string Register(string email, string password, string displayName)
        {
            try
            {
                using (var db = new TheWeakestRivalEntities())
                {
                    var account = new Accounts
                    {
                        email = email,
                        password_hash = System.Text.Encoding.UTF8.GetBytes(password),
                        status = 1,
                        created_at = DateTime.UtcNow
                    };
                    db.Accounts.Add(account);
                    db.SaveChanges();

                    var user = new Users
                    {
                        user_id = account.account_id,
                        display_name = displayName,
                        created_at = DateTime.UtcNow
                    };
                    db.Users.Add(user);
                    db.SaveChanges();

                    return "Usuario registrado correctamente";
                }
            }
            catch (Exception ex)
            {
                return "Error: " + ex.Message;
            }
        }
    }
}
