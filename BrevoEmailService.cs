
using System.Net.Http.Headers;
using System.Net.Http.Json;

public sealed class BrevoEmailService : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _senderEmail;
    private readonly string _senderName;

    public BrevoEmailService(
        string apiKey,
        string senderEmail,
        string senderName = "BTshop")
    {
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri("https://api.brevo.com/v3/")
        };

        _httpClient.DefaultRequestHeaders.Add(
            "api-key",
            apiKey);

        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));

        _senderEmail = senderEmail;
        _senderName = senderName;
    }

    public static string GetClientConfirmationHtml()
    {
        return """
            <!DOCTYPE html>
            <html lang="uk">
            <head>
                <meta charset="UTF-8">
                <meta name="viewport" content="width=device-width, initial-scale=1.0">
            </head>

            <body style="margin:0; padding:0; background-color:#eef3f9;
                         font-family:Arial, Helvetica, sans-serif;">

                <table width="100%" cellpadding="0" cellspacing="0"
                       style="background-color:#eef3f9; padding:35px 12px;">
                    <tr>
                        <td align="center">

                            <table width="100%" cellpadding="0" cellspacing="0"
                                   style="max-width:600px; background-color:#ffffff;
                                          border-radius:14px; overflow:hidden;">

                                <tr>
                                    <td style="height:6px;
                                               background-color:#1685e8;
                                               font-size:0;">
                                        &nbsp;
                                    </td>
                                </tr>

                                <tr>
                                    <td align="center"
                                        style="padding:28px 25px 12px;">
                                        <img
                                            src="https://i.postimg.cc/dVN48pCj/baner.jpg"
                                            alt="BT SHOP"
                                            width="420"
                                            style="display:block; width:100%;
                                                   max-width:420px; height:auto;
                                                   border:0;"
                                        />
                                    </td>
                                </tr>

                                <tr>
                                    <td style="padding:20px 35px 30px;
                                               color:#27364a;
                                               font-size:15px;
                                               line-height:1.8;">

                                        <h2 style="margin:0 0 22px;
                                                   color:#12345a;
                                                   font-size:25px;
                                                   font-weight:700;">
                                            Доброго дня!
                                        </h2>

                                        <div style="width:45px; height:3px;
                                                    background-color:#1685e8;
                                                    margin-bottom:25px;">
                                        </div>

                                        <p style="margin:0 0 18px;">
                                            Дякуємо, що обрали
                                            <strong style="color:#1685e8;">
                                                BTshop
                                            </strong>! 🤝 Нам дуже приємно,
                                            що ви довірили нам розвиток свого бізнесу.
                                        </p>

                                        <p style="margin:0 0 18px;">
                                            Ми готові допомогти вам досягати нових
                                            результатів і зробимо все можливе,
                                            щоб наша співпраця була комфортною
                                            та ефективною.
                                        </p>

                                        <p style="margin:0 0 18px;">
                                            Якщо виникнуть запитання —
                                            ми завжди на зв’язку!
                                        </p>

                                        <p style="margin:0 0 25px;">
                                            Дякуємо за довіру та до плідної співпраці!
                                        </p>

                                        <p style="margin:0; color:#52657c;">
                                            З повагою,<br />
                                            <strong style="color:#12345a;
                                                           font-size:16px;">
                                                Команда BTshop
                                            </strong>
                                        </p>

                                    </td>
                                </tr>

                                <tr>
                                    <td align="center"
                                        style="background-color:#12345a;
                                               padding:23px 20px;">

                                        <p style="margin:0 0 7px;
                                                  color:#ffffff;
                                                  font-size:17px;
                                                  font-weight:bold;
                                                  letter-spacing:2px;">
                                            BT SHOP
                                        </p>

                                        <p style="margin:0;
                                                  color:#b8cce2;
                                                  font-size:12px;">
                                            Дякуємо, що обираєте нас!
                                        </p>

                                    </td>
                                </tr>

                            </table>

                        </td>
                    </tr>
                </table>

            </body>
            </html>
            """;
    }


    public static string GetAdminNotificationHtml(
        string name,
        string company,
        string industry,
        string instagram,
        string telegram,
        string phone,
        string email,
        string services,
        string businessDetails,
        string productionDetails)
    {
        string Safe(string? value)
        {
            return System.Net.WebUtility.HtmlEncode(
                string.IsNullOrWhiteSpace(value) ? "Не вказано" : value);
        }

        return $"""
        <!DOCTYPE html>
        <html lang="uk">
        <body style="margin:0; padding:25px;
                     background-color:#eef3f9;
                     font-family:Arial,sans-serif;">

            <table width="100%" cellpadding="0" cellspacing="0"
                   style="max-width:600px; margin:auto;
                          background:#ffffff; border-radius:12px;
                          overflow:hidden;">

                <tr>
                    <td style="background:#12345a; padding:25px;
                               color:white; text-align:center;">
                        <h2 style="margin:0;">BT SHOP</h2>
                        <p style="margin:10px 0 0; color:#b8cce2;">
                            Нова заявка від клієнта 🔔
                        </p>
                    </td>
                </tr>

                <tr>
                    <td style="padding:25px; color:#27364a;
                               font-size:14px; line-height:1.8;">

                        <h3 style="color:#1685e8;">
                            Інформація про клієнта
                        </h3>

                        <p><b>Ім'я:</b> {Safe(name)}</p>
                        <p><b>Компанія:</b> {Safe(company)}</p>
                        <p><b>Сфера діяльності:</b> {Safe(industry)}</p>
                        <p><b>Телефон:</b> {Safe(phone)}</p>
                        <p><b>Email:</b> {Safe(email)}</p>
                        <p><b>Telegram:</b> {Safe(telegram)}</p>
                        <p><b>Instagram:</b> {Safe(instagram)}</p>

                        <hr style="border:0; border-top:1px solid #e5eaf0;">

                        <h3 style="color:#1685e8;">
                            Обрані послуги
                        </h3>

                        <p>{Safe(services)}</p>

                        <h3 style="color:#1685e8;">
                            Додаткова інформація
                        </h3>

                        <p><b>Бізнес:</b> {Safe(businessDetails)}</p>
                        <p><b>Виробництво:</b> {Safe(productionDetails)}</p>

                    </td>
                </tr>

                <tr>
                    <td style="background:#12345a; padding:18px;
                               text-align:center; color:#b8cce2;
                               font-size:12px;">
                        BT SHOP · Адміністративне сповіщення
                    </td>
                </tr>

            </table>

        </body>
        </html>
        """;
    }


    public async Task SendEmailAsync(
        string recipientEmail,
        string subject,
        string htmlContent,
        CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            sender = new
            {
                name = _senderName,
                email = _senderEmail
            },
            to = new[]
            {
                new { email = recipientEmail }
            },
            subject,
            htmlContent
        };

        using var response = await _httpClient.PostAsJsonAsync(
            "smtp/email",
            payload,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            string error = await response.Content.ReadAsStringAsync(
                cancellationToken);

            throw new InvalidOperationException(
                $"Brevo API error ({(int)response.StatusCode}): {error}");
        }
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
