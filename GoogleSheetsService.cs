
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;

public sealed class GoogleSheetsService
{
    private readonly SheetsService _sheetsService;
    private readonly string _spreadsheetId;

    public GoogleSheetsService(string credentialsPath, string spreadsheetId)
    {
        GoogleCredential credential = GoogleCredential
            .FromFile(credentialsPath)
            .CreateScoped(SheetsService.Scope.Spreadsheets);

        _sheetsService = new SheetsService(
            new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = "BTshopBot"
            });

        _spreadsheetId = spreadsheetId;
    }

    public async Task AppendApplicationAsync(
     string name,
     string company,
     string industry,
     string instagram,
     string telegram,
     string phone,
     string email,
     string services,
     string businessDetails,
     string productionDetails,
     CancellationToken cancellationToken = default)
    {
        // Читаем все строки, начиная со второй.
        var readRequest = _sheetsService.Spreadsheets.Values.Get(
            _spreadsheetId,
            "'Лист1'!A2:J");

        var response = await readRequest.ExecuteAsync(cancellationToken);
        var rows = response.Values;

        // Определяем следующую строку по последней непустой строке A:J.
        int nextRow = 2;

        if (rows != null)
        {
            for (int i = rows.Count - 1; i >= 0; i--)
            {
                bool hasData = rows[i].Any(value =>
                    !string.IsNullOrWhiteSpace(value?.ToString()));

                if (hasData)
                {
                    nextRow = i + 3;
                    break;
                }
            }
        }

        string telegramValue = telegram.Trim();

        if (!string.IsNullOrWhiteSpace(telegramValue))
        {
            telegramValue = telegramValue.TrimStart('@');

            telegramValue =
                $"=HYPERLINK(\"https://t.me/{telegramValue}\"; \"@{telegramValue}\")";
        }

        var values = new List<IList<object>>
    {
        new List<object>
        {
            name,
            company,
            industry,
            instagram,
            telegramValue,
            phone,
            email,
            services,
            businessDetails,
            productionDetails
        }
    };

        // Дополнительная проверка: целевая строка должна быть пустой.
        var checkRequest = _sheetsService.Spreadsheets.Values.Get(
            _spreadsheetId,
            $"'Лист1'!A{nextRow}:J{nextRow}");

        var checkResponse = await checkRequest.ExecuteAsync(cancellationToken);

        if (checkResponse.Values != null &&
            checkResponse.Values.Any(row =>
                row.Any(value =>
                    !string.IsNullOrWhiteSpace(value?.ToString()))))
        {
            throw new InvalidOperationException(
                $"Рядок {nextRow} вже містить дані. Запис скасовано.");
        }

        var updateRequest = _sheetsService.Spreadsheets.Values.Update(
            new ValueRange { Values = values },
            _spreadsheetId,
            $"'Лист1'!A{nextRow}:J{nextRow}");

        updateRequest.ValueInputOption =
            SpreadsheetsResource.ValuesResource.UpdateRequest
                .ValueInputOptionEnum.USERENTERED;

        await updateRequest.ExecuteAsync(cancellationToken);

        Console.WriteLine($"DEBUG: Application saved to row {nextRow}.");
    }
}
