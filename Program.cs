using System.Collections.Concurrent;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

string? token = Environment.GetEnvironmentVariable("BTSHOP_TELEGRAM_TOKEN");

if (string.IsNullOrWhiteSpace(token))
{
    Console.WriteLine("ERROR: Telegram token not found.");
    return;
}

var bot = new TelegramBotClient(token);
var me = await bot.GetMe();

Console.WriteLine($"Bot started: @{me.Username}");
Console.WriteLine("Press Ctrl+C to stop the bot.");

using var cancellationTokenSource = new CancellationTokenSource();
var clientForms = new ConcurrentDictionary<long, ClientForm>();

bot.StartReceiving(
    HandleUpdateAsync,
    HandlePollingErrorAsync,
    new ReceiverOptions
    {
        AllowedUpdates = Array.Empty<UpdateType>()
    },
    cancellationTokenSource.Token
);

await Task.Delay(Timeout.Infinite, cancellationTokenSource.Token);

async Task HandleUpdateAsync(
    ITelegramBotClient botClient,
    Update update,
    CancellationToken cancellationToken)
{
    if (update.Message is not { } message)
        return;

    long chatId = message.Chat.Id;
    string? messageText = message.Text?.Trim();

    if (messageText == "/cancel" || messageText == "❌ Скасувати")
    {
        clientForms.TryRemove(chatId, out _);
        await botClient.SendMessage(
            chatId,
            "Анкету скасовано. Щоб почати знову, натисни /start.",
            replyMarkup: new ReplyKeyboardRemove(),
            cancellationToken: cancellationToken);
        return;
    }

    if (messageText == "/start")
    {
        clientForms.TryRemove(chatId, out _);
        await botClient.SendMessage(
            chatId,
            "Привіт! 👋\n\n" +
            "Ласкаво просимо до BTshop!\n" +
            "Залиш заявку, і ми зв'яжемося з тобою щодо розвитку бізнесу.",
            replyMarkup: new ReplyKeyboardMarkup(
                new[] { new KeyboardButton[] { "📝 Залишити заявку" } })
            {
                ResizeKeyboard = true
            },
            cancellationToken: cancellationToken);
        return;
    }

    if (messageText == "📝 Залишити заявку")
    {
        var form = new ClientForm();
        clientForms[chatId] = form;
        await AskForFieldAsync(botClient, chatId, form, FormStep.Name, cancellationToken);
        return;
    }

    if (!clientForms.TryGetValue(chatId, out var currentForm))
        return;

    // Контакт можна прийняти лише від самого користувача.
    if (message.Contact is { } contact)
    {
        if (message.From is null || contact.UserId != message.From.Id)
        {
            await botClient.SendMessage(
                chatId,
                "Будь ласка, поділися саме своїм контактом.",
                cancellationToken: cancellationToken);
            return;
        }

        currentForm.Phone = contact.PhoneNumber;
        currentForm.Telegram = message.From.Username is { } username ? $"@{username}" : "";

        if (currentForm.EditingField.HasValue)
        {
            currentForm.EditingField = null;
            currentForm.Step = FormStep.Completed;
            await ShowSummaryAsync(botClient, chatId, currentForm, cancellationToken);
            return;
        }

        if (currentForm.Step == FormStep.Telegram)
        {
            currentForm.Step = FormStep.Instagram;
            await AskForFieldAsync(botClient, chatId, currentForm, FormStep.Instagram, cancellationToken);
            return;
        }

        if (currentForm.Step == FormStep.Phone)
        {
            currentForm.Step = FormStep.Completed;
            await ShowSummaryAsync(botClient, chatId, currentForm, cancellationToken);
        }

        return;
    }

    if (messageText is null)
        return;

    string answer = messageText;

    if (currentForm.EditingField.HasValue)
    {
        FormStep field = currentForm.EditingField.Value;
        SetField(currentForm, field, answer == "⏭ Пропустити" ? "" : answer);
        currentForm.EditingField = null;
        currentForm.Step = FormStep.Completed;
        await ShowSummaryAsync(botClient, chatId, currentForm, cancellationToken);
        return;
    }

    if (currentForm.Step == FormStep.Completed)
    {
        if (answer == "✏️ Редагувати")
        {
            await botClient.SendMessage(
                chatId,
                "Які дані хочеш змінити?",
                replyMarkup: EditKeyboard(),
                cancellationToken: cancellationToken);
            return;
        }

        if (answer == "⬅️ Назад")
        {
            await ShowSummaryAsync(botClient, chatId, currentForm, cancellationToken);
            return;
        }

        if (TryGetFieldFromButton(answer, out var selectedField))
        {
            currentForm.EditingField = selectedField;
            await AskForFieldAsync(botClient, chatId, currentForm, selectedField, cancellationToken);
            return;
        }

        if (answer == "✅ Підтвердити")
        {
            bool hasAnyData = new[]
            {
                currentForm.Name,
                currentForm.Company,
                currentForm.Industry,
                currentForm.Telegram,
                currentForm.Instagram,
                currentForm.Phone
            }.Any(value => !string.IsNullOrWhiteSpace(value));

            if (!hasAnyData)
            {
                await botClient.SendMessage(
                    chatId,
                    "Анкета порожня. Будь ласка, заповни хоча б одне поле, щоб ми могли прийняти заявку.",
                    cancellationToken: cancellationToken);
                return;
            }

            // Тимчасово дані виводяться в консоль. Google Sheets ще не підключено.
            Console.WriteLine(
                $"Заявка від Telegram ID {chatId}: " +
                $"Ім'я={currentForm.Name}; Компанія={currentForm.Company}; " +
                $"Сфера={currentForm.Industry}; Telegram={currentForm.Telegram}; " +
                $"Instagram={currentForm.Instagram}; Телефон={currentForm.Phone}");

            clientForms.TryRemove(chatId, out _);
            await botClient.SendMessage(
                chatId,
                "Дякуємо! 💚\n\nТвою заявку прийнято. Ми зв'яжемося з тобою найближчим часом!",
                replyMarkup: new ReplyKeyboardRemove(),
                cancellationToken: cancellationToken);
            return;
        }

        await botClient.SendMessage(
            chatId,
            "Будь ласка, обери дію за допомогою кнопок.",
            cancellationToken: cancellationToken);
        return;
    }

    if (answer == "⏭ Пропустити")
    {
        SetField(currentForm, currentForm.Step, "");
        await AdvanceFormAsync(botClient, chatId, currentForm, cancellationToken);
        return;
    }

    SetField(currentForm, currentForm.Step, answer);
    await AdvanceFormAsync(botClient, chatId, currentForm, cancellationToken);
}

async Task AdvanceFormAsync(
    ITelegramBotClient botClient,
    long chatId,
    ClientForm form,
    CancellationToken cancellationToken)
{
    switch (form.Step)
    {
        case FormStep.Name:
            form.Step = FormStep.Company;
            break;
        case FormStep.Company:
            form.Step = FormStep.Industry;
            break;
        case FormStep.Industry:
            form.Step = FormStep.Telegram;
            break;
        case FormStep.Telegram:
            form.Step = FormStep.Instagram;
            break;
        case FormStep.Instagram:
            form.Step = string.IsNullOrWhiteSpace(form.Phone) ? FormStep.Phone : FormStep.Completed;
            if (form.Step == FormStep.Completed)
            {
                await ShowSummaryAsync(botClient, chatId, form, cancellationToken);
                return;
            }
            break;
        case FormStep.Phone:
            form.Step = FormStep.Completed;
            await ShowSummaryAsync(botClient, chatId, form, cancellationToken);
            return;
        default:
            return;
    }

    await AskForFieldAsync(botClient, chatId, form, form.Step, cancellationToken);
}

async Task AskForFieldAsync(
    ITelegramBotClient botClient,
    long chatId,
    ClientForm form,
    FormStep field,
    CancellationToken cancellationToken)
{
    form.Step = field;
    string question = field switch
    {
        FormStep.Name => "Як до тебе звертатися? Введи ім'я та прізвище.",
        FormStep.Company => "Вкажи назву компанії.",
        FormStep.Industry => "Чим займається твоя компанія або бізнес?",
        FormStep.Telegram => "Можеш поділитися своїм контактом кнопкою нижче або ввести Telegram вручну.",
        FormStep.Instagram => "Вкажи свій Instagram.",
        FormStep.Phone => "Вкажи номер телефону або поділися контактом кнопкою нижче.",
        _ => "Введи відповідь."
    };

    KeyboardButton[][] buttons;
    if (field is FormStep.Telegram or FormStep.Phone)
    {
        buttons = new[]
        {
            new KeyboardButton[] { KeyboardButton.WithRequestContact("📲 Поділитися контактом") },
            new KeyboardButton[] { "⏭ Пропустити" }
        };
    }
    else
    {
        buttons = new[] { new KeyboardButton[] { "⏭ Пропустити" } };
    }

    await botClient.SendMessage(
        chatId,
        question,
        replyMarkup: new ReplyKeyboardMarkup(buttons) { ResizeKeyboard = true },
        cancellationToken: cancellationToken);
}

async Task ShowSummaryAsync(
    ITelegramBotClient botClient,
    long chatId,
    ClientForm form,
    CancellationToken cancellationToken)
{
    form.Step = FormStep.Completed;
    string summary =
        "Перевір свої дані:\n\n" +
        $"Ім'я: {Display(form.Name)}\n" +
        $"Компанія: {Display(form.Company)}\n" +
        $"Сфера діяльності: {Display(form.Industry)}\n" +
        $"Telegram: {Display(form.Telegram)}\n" +
        $"Instagram: {Display(form.Instagram)}\n" +
        $"Телефон: {Display(form.Phone)}\n\n" +
        "Можеш підтвердити заявку або відредагувати дані.";

    await botClient.SendMessage(
        chatId,
        summary,
        replyMarkup: new ReplyKeyboardMarkup(
            new[]
            {
                new KeyboardButton[] { "✅ Підтвердити", "✏️ Редагувати" },
                new KeyboardButton[] { "❌ Скасувати" }
            })
        {
            ResizeKeyboard = true
        },
        cancellationToken: cancellationToken);
}

ReplyKeyboardMarkup EditKeyboard() =>
    new(new[]
    {
        new KeyboardButton[] { "👤 Ім'я та прізвище", "🏢 Компанія" },
        new KeyboardButton[] { "🏭 Сфера діяльності", "✈️ Telegram" },
        new KeyboardButton[] { "📸 Instagram", "📞 Телефон" },
        new KeyboardButton[] { "⬅️ Назад" }
    })
    {
        ResizeKeyboard = true
    };

bool TryGetFieldFromButton(string answer, out FormStep field)
{
    field = answer switch
    {
        "👤 Ім'я та прізвище" => FormStep.Name,
        "🏢 Компанія" => FormStep.Company,
        "🏭 Сфера діяльності" => FormStep.Industry,
        "✈️ Telegram" => FormStep.Telegram,
        "📸 Instagram" => FormStep.Instagram,
        "📞 Телефон" => FormStep.Phone,
        _ => FormStep.Completed
    };

    return field != FormStep.Completed;
}

void SetField(ClientForm form, FormStep field, string value)
{
    switch (field)
    {
        case FormStep.Name:
            form.Name = value;
            break;
        case FormStep.Company:
            form.Company = value;
            break;
        case FormStep.Industry:
            form.Industry = value;
            break;
        case FormStep.Telegram:
            form.Telegram = value;
            break;
        case FormStep.Instagram:
            form.Instagram = value;
            break;
        case FormStep.Phone:
            form.Phone = value;
            break;
    }
}

ReplyKeyboardMarkup SkipKeyboard() =>
    new(new[] { new KeyboardButton[] { "⏭ Пропустити" } })
    {
        ResizeKeyboard = true
    };

string Display(string value) =>
    string.IsNullOrWhiteSpace(value) || value == "⏭ Пропустити"
        ? "Не вказано"
        : value;

Task HandlePollingErrorAsync(
    ITelegramBotClient botClient,
    Exception exception,
    CancellationToken cancellationToken)
{
    Console.WriteLine($"Telegram error: {exception.Message}");
    return Task.CompletedTask;
}

enum FormStep
{
    Name,
    Company,
    Industry,
    Telegram,
    Instagram,
    Phone,
    Completed
}

class ClientForm
{
    public FormStep Step { get; set; } = FormStep.Name;
    public FormStep? EditingField { get; set; }
    public string Name { get; set; } = "";
    public string Company { get; set; } = "";
    public string Industry { get; set; } = "";
    public string Telegram { get; set; } = "";
    public string Instagram { get; set; } = "";
    public string Phone { get; set; } = "";
}
