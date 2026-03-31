using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

namespace Seguimiento.Services
{
    public class EmailService
    {
        public async Task EnviarCorreoAsync(string asunto, string mensaje, string[] destinatarios)
        {
            var smtp = new SmtpClient("tramites.lozanoasesores.com")
            {
                Port = 587, // 🔥 CAMBIO CLAVE
                Credentials = new NetworkCredential(
         "embarques@tramites.lozanoasesores.com",
         "Lozano2025.*"
     ),
                EnableSsl = true,
                UseDefaultCredentials = false
            };

            var mail = new MailMessage
            {
                From = new MailAddress("embarques@tramites.lozanoasesores.com", "Sistema Embarques"),
                Subject = asunto,
                Body = mensaje,
                IsBodyHtml = true
            };

            foreach (var correo in destinatarios)
            {
                mail.To.Add(correo);
            }

            await smtp.SendMailAsync(mail);
        }
    }
}