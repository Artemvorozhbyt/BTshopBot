
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
    string services,
    string businessDetails,
    string productionDetails,
    CancellationToken cancellationToken = default)
    {
        // Читаем столбец A, чтобы найти последнего клиента.
        var readRequest = _sheetsService.Spreadsheets.Values.Get(
            _spreadsheetId,
            "'Лист1'!A2:A");

        var response = await readRequest.ExecuteAsync(cancellationToken);

        var valuesInColumn = response.Values;
        int nextRow = 2;

        if (valuesInColumn != null)
        {
            nextRow = valuesInColumn.Count + 2;
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
                services,
                businessDetails,
                productionDetails
            }
        };

        var updateRequest = _sheetsService.Spreadsheets.Values.Update(
            new ValueRange { Values = values },
            _spreadsheetId,
            $"'Лист1'!A{nextRow}:I{nextRow}");

        updateRequest.ValueInputOption =
            SpreadsheetsResource.ValuesResource.UpdateRequest
                .ValueInputOptionEnum.USERENTERED;

        await updateRequest.ExecuteAsync(cancellationToken);

        Console.WriteLine($"DEBUG: Application saved to row {nextRow}.");
    }
}
