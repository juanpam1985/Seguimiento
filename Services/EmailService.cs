using System;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;

namespace Seguimiento.Services
{
    public class EmailService
    {
        public async Task EnviarCorreoAsync(string asunto, string mensaje, string[] destinatarios)
        {
            if (destinatarios == null || destinatarios.Length == 0)
                throw new InvalidOperationException("No existen destinatarios configurados para el envío.");

            using var smtp = new SmtpClient("tramites.lozanoasesores.com")
            {
                Port = 587,
                Credentials = new NetworkCredential(
                    "embarques@tramites.lozanoasesores.com",
                    "Lozano2025.*"
                ),
                EnableSsl = true,
                UseDefaultCredentials = false,
                DeliveryMethod = SmtpDeliveryMethod.Network
            };

            using var mail = new MailMessage
            {
                From = new MailAddress(
                    "embarques@tramites.lozanoasesores.com",
                    "Sistema Embarques",
                    Encoding.UTF8
                ),
                Subject = asunto,
                Body = mensaje,
                IsBodyHtml = true,
                SubjectEncoding = Encoding.UTF8,
                BodyEncoding = Encoding.UTF8
            };

            foreach (var correo in destinatarios
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                mail.To.Add(correo);
            }

            if (mail.To.Count == 0)
                throw new InvalidOperationException("No existen correos válidos para enviar el informe.");

            await smtp.SendMailAsync(mail);
        }
    }
}