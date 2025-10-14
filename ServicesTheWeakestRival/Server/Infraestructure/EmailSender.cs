using System;
using System.Configuration;
using System.Net;
using System.Net.Mail;
using System.Text;

namespace ServicesTheWeakestRival.Server.Infrastructure
{
    internal static class EmailSender
    {
        private static readonly string Host = ConfigurationManager.AppSettings["SmtpHost"] ?? "smtp.gmail.com";
        private static readonly int Port = int.TryParse(ConfigurationManager.AppSettings["SmtpPort"], out var p) ? p : 587;
        private static readonly string User = ConfigurationManager.AppSettings["SmtpUser"];
        private static readonly string Pass = ConfigurationManager.AppSettings["SmtpPass"];
        private static readonly string From = ConfigurationManager.AppSettings["EmailFrom"] ?? (User ?? "no-reply@localhost");

        public static void SendVerificationCode(string toEmail, string code, int ttlMinutes)
        {
            using (var smtp = new SmtpClient(Host, Port))
            {
                smtp.EnableSsl = true;
                smtp.Credentials = new NetworkCredential(User, Pass);

                var subject = "Tu código de verificación";
                var body = new StringBuilder()
                    .AppendLine("Hola,")
                    .AppendLine()
                    .AppendLine($"Tu código de verificación es: {code}")
                    .AppendLine($"Caduca en {ttlMinutes} minutos.")
                    .AppendLine()
                    .AppendLine("Si no solicitaste este código, ignora este correo.")
                    .ToString();

                using (var msg = new MailMessage(From, toEmail, subject, body))
                {
                    smtp.Send(msg);
                }
            }
        }
    }
}
