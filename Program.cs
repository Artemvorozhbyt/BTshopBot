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
var adminClientForms =
    new ConcurrentDictionary<long, AdminClientForm>();
const long AdminTelegramId = 5841248869;

string spreadsheetId = "1X-M2lHAZBCtTfahu_54TsF0U8y7d0MS0qVV4hq0xbgc";

string credentialsPath = Path.Combine(
    Directory.GetCurrentDirectory(),
    "secrets",
    "google-service-account.json"
);

var googleSheetsService = new GoogleSheetsService(
    credentialsPath,
    spreadsheetId
);

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

    if (message.From is { } sender)
    {
        Console.WriteLine(
            $"Telegram ID: {sender.Id}, Username: @{sender.Username}");
    }

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

    if (messageText == "/help" || messageText == "❓ Допомога")
    {
        await botClient.SendMessage(
            chatId,
            "Потрібна допомога або щось незрозуміло? 😊\n\n" +
            "Напиши Дімі — він допоможе розібратися з послугами BTshop та заповненням заявки.",
            replyMarkup: new InlineKeyboardMarkup(
                InlineKeyboardButton.WithUrl(
                    "💬 Написати Дмитру",
                    "https://t.me/dmytro_bazeniuk")),
            cancellationToken: cancellationToken);

        return;
    }

    if (messageText == "/admin")
    {
        if (message.From?.Id != AdminTelegramId)
        {
            await botClient.SendMessage(
                chatId,
                "⛔ У тебе немає доступу до адмін-панелі.",
                cancellationToken: cancellationToken);

            return;
        }

        await botClient.SendMessage(
            chatId,
            "🔐 Адмін-панель BTshop\n\nОбери дію:",
            replyMarkup: new ReplyKeyboardMarkup(
                new[]
                {
                    new KeyboardButton[] { "➕ Додати клієнта" },
                    new KeyboardButton[] { "❌ Закрити адмін-панель" }
                })
                {
                    ResizeKeyboard = true,
                    IsPersistent = true
                },
            cancellationToken: cancellationToken);

        return;
    }
    if (messageText == "❌ Закрити адмін-панель")
    {
        if (message.From?.Id != AdminTelegramId)
        {
            await botClient.SendMessage(
                chatId,
                "⛔ У тебе немає доступу до цієї дії.",
                cancellationToken: cancellationToken);

            return;
        }

        await botClient.SendMessage(
            chatId,
            "Адмін-панель закрита. Щоб відкрити її знову, введи /admin.",
            replyMarkup: new ReplyKeyboardMarkup(
                new[]
                {
                new KeyboardButton[] { "📝 Залишити заявку" },
                new KeyboardButton[] { "❓ Допомога" }
                })
            {
                ResizeKeyboard = true,
                IsPersistent = true
            },
            cancellationToken: cancellationToken);

        return;
    }
    if (messageText == "➕ Додати клієнта")
    {
        if (message.From?.Id != AdminTelegramId)
        {
            await botClient.SendMessage(
                chatId,
                "⛔ У тебе немає доступу до цієї дії.",
                cancellationToken: cancellationToken);

            return;
        }

        var newAdminForm = new AdminClientForm();
        adminClientForms[chatId] = newAdminForm;

        await botClient.SendMessage(
            chatId,
            "➕ Додавання клієнта\n\nВведи ім'я та прізвище клієнта.",
            replyMarkup: new ReplyKeyboardMarkup(
                new[]
                {
                    new KeyboardButton[] { "❌ Скасувати додавання" }
                })
            {
                ResizeKeyboard = true,
                IsPersistent = true
            },
            cancellationToken: cancellationToken);

        return;
    }

    if (messageText == "❌ Скасувати додавання")
    {
        if (message.From?.Id != AdminTelegramId)
        {
            await botClient.SendMessage(
                chatId,
                "⛔ У тебе немає доступу до цієї дії.",
                cancellationToken: cancellationToken);

            return;
        }

        adminClientForms.TryRemove(chatId, out _);

        await botClient.SendMessage(
            chatId,
            "Додавання клієнта скасовано.",
            replyMarkup: new ReplyKeyboardMarkup(
                new[]
                {
                    new KeyboardButton[] { "➕ Додати клієнта" },
                    new KeyboardButton[] { "❌ Закрити адмін-панель" }
                })
            {
                ResizeKeyboard = true,
                IsPersistent = true
            },
            cancellationToken: cancellationToken);

        return;
    }

    if (adminClientForms.TryGetValue(chatId, out var adminForm))
    {
        if (message.From?.Id != AdminTelegramId)
            return;


        if (adminForm.IsSelectingServices)
        {
            if (messageText == "❌ Скасувати додавання")
            {
                adminClientForms.TryRemove(chatId, out _);

                await botClient.SendMessage(
                    chatId,
                    "Додавання клієнта скасовано.",
                    cancellationToken: cancellationToken);

                return;
            }


            if (messageText == "✅ Готово")
            {
                if (adminForm.SelectedServices.Count == 0)
                {
                    await botClient.SendMessage(
                        chatId,
                        "Спочатку обери хоча б одну послугу.",
                        cancellationToken: cancellationToken);

                    return;
                }

                adminForm.IsSelectingServices = false;

                try
                {
                    await googleSheetsService.AppendApplicationAsync(
                        name: adminForm.Name,
                        company: adminForm.Company,
                        industry: adminForm.Industry,
                        instagram: adminForm.Instagram,
                        telegram: adminForm.Telegram,
                        phone: adminForm.Phone,
                        services: string.Join(", ", adminForm.SelectedServices),
                        businessDetails: adminForm.BusinessDetails,
                        productionDetails: adminForm.ProductionDetails,
                        cancellationToken: cancellationToken);

                    adminClientForms.TryRemove(chatId, out _);

                    await botClient.SendMessage(
                        chatId,
                        "✅ Клієнта успішно додано до Google Sheets!",
                        replyMarkup: new ReplyKeyboardMarkup(
                        new[]
                        {
                            new KeyboardButton("➕ Додати клієнта"),
                            new KeyboardButton("❌ Закрити адмін-панель")
                        })
                        {
                            ResizeKeyboard = true
                        },
                        cancellationToken: cancellationToken);
                }
                catch (Exception ex)
                {
                    adminForm.IsSelectingServices = true;

                    Console.WriteLine($"Помилка збереження клієнта: {ex}");

                    await botClient.SendMessage(
                        chatId,
                        "❌ Не вдалося зберегти клієнта. Спробуй ще раз натиснути «✅ Готово».",
                        cancellationToken: cancellationToken);
                }

                return;
            }


            if (messageText is "📦 Повний пакет BTshop"
                or "🚢 Імпорт товарів"
                or "🛒 Супровід e-commerce"
                or "➕ Інша послуга")
            {
                if (messageText == "📦 Повний пакет BTshop")
                {
                    adminForm.SelectedServices.Clear();
                    adminForm.SelectedServices.Add(messageText);
                }
                else
                {
                    adminForm.SelectedServices.Remove("📦 Повний пакет BTshop");

                    if (!adminForm.SelectedServices.Remove(messageText))
                    {
                        adminForm.SelectedServices.Add(messageText);
                    }
                }

                var selectedServices = adminForm.SelectedServices.Count > 0
                    ? string.Join(", ", adminForm.SelectedServices)
                    : "поки не обрано";

                await botClient.SendMessage(
                    chatId,
                    $"Обрані послуги: {selectedServices}",
                    cancellationToken: cancellationToken);

                return;
            }
        }


        if (string.IsNullOrWhiteSpace(adminForm.Name))
        {
            adminForm.Name = messageText ?? "";

            await botClient.SendMessage(
                chatId,
                "Вкажи назву компанії клієнта.",
                cancellationToken: cancellationToken);

            return;
        }

        if (string.IsNullOrWhiteSpace(adminForm.Company))
        {
            adminForm.Company = messageText ?? "";

            await botClient.SendMessage(
                chatId,
                "Вкажи сферу діяльності клієнта.",
                replyMarkup: new ReplyKeyboardMarkup(
                    new[]
                    {
                    new KeyboardButton[] { "🏭 Виробництво" },
                    new KeyboardButton[] { "🛒 Продажі в інтернеті" },
                    new KeyboardButton[] { "📦 Оптові продажі" },
                    new KeyboardButton[] { "🏪 Роздрібні продажі" },
                    new KeyboardButton[] { "✏️ Інше" },
                    new KeyboardButton[] { "❌ Скасувати додавання" }
                    })
                {
                    ResizeKeyboard = true,
                    IsPersistent = true
                },
                cancellationToken: cancellationToken);

            return;
        }

        if (string.IsNullOrWhiteSpace(adminForm.Industry))
        {
            adminForm.Industry = messageText ?? "";

            await botClient.SendMessage(
                chatId,
                "Вкажи Telegram клієнта (наприклад, @username) або введи «-», якщо його немає.",
                cancellationToken: cancellationToken);

            return;
        }


        if (string.IsNullOrWhiteSpace(adminForm.Telegram))
        {
            adminForm.Telegram = messageText ?? "";

            await botClient.SendMessage(
                chatId,
                "Вкажи Instagram клієнта (наприклад, @username) або введи «-», якщо його немає.",
                cancellationToken: cancellationToken);

            return;
        }



        if (string.IsNullOrWhiteSpace(adminForm.Instagram))
        {
            adminForm.Instagram = messageText == "-"
                ? "-"
                : messageText ?? "";

            await botClient.SendMessage(
                chatId,
                "Вкажи номер телефону клієнта або введи «-», якщо його немає.",
                cancellationToken: cancellationToken);

            return;
        }


        if (string.IsNullOrWhiteSpace(adminForm.Phone))
        {
            adminForm.Phone = messageText == "-" ? "" : messageText ?? "";
            adminForm.IsSelectingServices = true;

            await botClient.SendMessage(
                chatId,
                "Вкажи послуги BTshop, які цікавлять клієнта.",
                replyMarkup: new ReplyKeyboardMarkup(
                    new[]
                    {
                        new KeyboardButton[] { "📦 Повний пакет BTshop" },
                        new KeyboardButton[] { "🚢 Імпорт товарів" },
                        new KeyboardButton[] { "🛒 Супровід e-commerce" },
                        new KeyboardButton[] { "➕ Інша послуга" },
                        new KeyboardButton[] { "✅ Готово" },
                        new KeyboardButton[] { "❌ Скасувати додавання" }
                    })
                {
                    ResizeKeyboard = true,
                    IsPersistent = true
                },
                cancellationToken: cancellationToken);

            return;
        }
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
                new[]
                {
                    new KeyboardButton[] { "📝 Залишити заявку" },
                    new KeyboardButton[] { "❓ Допомога" }
                })
            {
                ResizeKeyboard = true,
                IsPersistent = true
            },
            cancellationToken: cancellationToken);
        return;
    }

    if (messageText == "📝 Залишити заявку")
    {
        var form = new ClientForm();
        clientForms[chatId] = form;

        await AskForFieldAsync(
            botClient,
            chatId,
            form,
            FormStep.Services,
            cancellationToken
        );

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
    if (currentForm.Step == FormStep.Services)
    {
        if (answer == "⏭ Пропустити")
        {
            currentForm.SelectedServices.Clear();

            await AdvanceFormAsync(
                botClient,
                chatId,
                currentForm,
                cancellationToken);

            return;
        }

        if (answer == "✅ Готово")
        {
            if (currentForm.SelectedServices.Count == 0)
            {
                await botClient.SendMessage(
                    chatId,
                    "Будь ласка, обери хоча б одну послугу або натисни «⏭ Пропустити».",
                    cancellationToken: cancellationToken);

                return;
            }

            await AdvanceFormAsync(
                botClient,
                chatId,
                currentForm,
                cancellationToken);

            return;
        }

        if (answer == "📦 Повний пакет BTshop")
        {
            currentForm.SelectedServices.Clear();
            currentForm.SelectedServices.Add("Повний пакет BTshop");
        }
        else if (answer == "🚢 Імпорт товарів")
        {
            currentForm.SelectedServices.Remove("Повний пакет BTshop");

            if (!currentForm.SelectedServices.Remove("Імпорт товарів"))
            {
                currentForm.SelectedServices.Add("Імпорт товарів");
            }
        }
        else if (answer == "🛒 Супровід e-commerce")
        {
            currentForm.SelectedServices.Remove("Повний пакет BTshop");

            if (!currentForm.SelectedServices.Remove("Супровід e-commerce"))
            {
                currentForm.SelectedServices.Add("Супровід e-commerce");
            }
        }
        else if (answer == "➕ Інша послуга")
        {
            currentForm.SelectedServices.Remove("Повний пакет BTshop");

            if (!currentForm.SelectedServices.Remove("Інша послуга"))
            {
                currentForm.SelectedServices.Add("Інша послуга");
            }
        }
        else
        {
            await botClient.SendMessage(
                chatId,
                "Будь ласка, обери послугу за допомогою кнопок.",
                cancellationToken: cancellationToken);

            return;
        }

        string selectedServices = currentForm.SelectedServices.Count > 0
            ? string.Join("\n", currentForm.SelectedServices.Select(s => $"• {s}"))
            : "Поки нічого не вибрано.";

        await botClient.SendMessage(
            chatId,
            $"Твій вибір:\n{selectedServices}\n\n" +
            "Можеш вибрати ще послуги. Коли закінчиш, натисни «✅ Готово».",
            replyMarkup: new ReplyKeyboardMarkup(new[]
            {
            new KeyboardButton[] { "📦 Повний пакет BTshop" },
            new KeyboardButton[] { "🚢 Імпорт товарів" },
            new KeyboardButton[] { "🛒 Супровід e-commerce" },
            new KeyboardButton[] { "➕ Інша послуга" },
            new KeyboardButton[] { "✅ Готово", "⏭ Пропустити" },
            new KeyboardButton[] { "❓ Допомога" }
            })
            {
                ResizeKeyboard = true,
                IsPersistent = true
            },
            cancellationToken: cancellationToken);

        return;
    }

    if (answer == "/skip")
    {
        if (currentForm.EditingField.HasValue)
        {
            SetField(currentForm, currentForm.EditingField.Value, "");
            currentForm.EditingField = null;
            currentForm.Step = FormStep.Completed;

            await ShowSummaryAsync(
                botClient,
                chatId,
                currentForm,
                cancellationToken
            );

            return;
        }

        if (currentForm.Step != FormStep.Completed)
        {
            SetField(currentForm, currentForm.Step, "");
            await AdvanceFormAsync(
                botClient,
                chatId,
                currentForm,
                cancellationToken
            );
        }

        return;
    }

    if (currentForm.EditingField.HasValue)
    {
        FormStep field = currentForm.EditingField.Value;

        SetField(currentForm, field, answer == "⏭ Пропустити" ? "" : answer);
        currentForm.EditingField = null;

        if (field == FormStep.Industry &&
            currentForm.Industry == "🏭 Виробництво")
        {
            await AskForFieldAsync(
                botClient,
                chatId,
                currentForm,
                FormStep.ProductionDetails,
                cancellationToken);

            return;
        }

        if (field == FormStep.Industry &&
            currentForm.SelectedServices.Contains("Супровід e-commerce"))
        {
            await AskForFieldAsync(
                botClient,
                chatId,
                currentForm,
                FormStep.BusinessDetails,
                cancellationToken);

            return;
        }

        currentForm.Step = FormStep.Completed;

        await ShowSummaryAsync(
            botClient,
            chatId,
            currentForm,
            cancellationToken);

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
                currentForm.Phone,
                string.Join(", ", currentForm.SelectedServices),
                currentForm.BusinessDetails,
                currentForm.ProductionDetails
            }.Any(value => !string.IsNullOrWhiteSpace(value));

            if (!hasAnyData)
            {
                await botClient.SendMessage(
                    chatId,
                    "Анкета порожня. Будь ласка, заповни хоча б одне поле, щоб ми могли прийняти заявку.",
                    cancellationToken: cancellationToken);
                return;
            }


            try
            {
                Console.WriteLine("DEBUG: User confirmed the application.");
                Console.WriteLine("DEBUG: Sending application to Google Sheets...");

                await googleSheetsService.AppendApplicationAsync(
                    currentForm.Name,
                    currentForm.Company,
                    currentForm.Industry,
                    currentForm.Instagram,
                    currentForm.Telegram,
                    currentForm.Phone,
                    string.Join(", ", currentForm.SelectedServices),
                    currentForm.BusinessDetails,
                    currentForm.ProductionDetails,
                    cancellationToken
                );

                Console.WriteLine("DEBUG: Application successfully saved to Google Sheets.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Google Sheets error: {ex}");

                await botClient.SendMessage(
                    chatId,
                    "Не вдалося зберегти заявку. Спробуй підтвердити її ще раз трохи пізніше.",
                    cancellationToken: cancellationToken
                );

                return;
            }


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
        case FormStep.Services:
            form.Step = FormStep.Name;
            break;
        case FormStep.Name:
            form.Step = FormStep.Company;
            break;
        case FormStep.Company:
            form.Step = FormStep.Industry;
            break;
        case FormStep.Industry:
            if (form.Industry == "🏭 Виробництво")
            {
                form.Step = FormStep.ProductionDetails;
            }
            else if (form.SelectedServices.Contains("Супровід e-commerce"))
            {
                form.Step = FormStep.BusinessDetails;
            }
            else
            {
                form.Step = FormStep.Telegram;
            }
            break;

        case FormStep.ProductionDetails:
        case FormStep.BusinessDetails:
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
        FormStep.Services => "Які послуги BTshop тебе цікавлять? Обери варіант нижче.",
        FormStep.Name => "Як до тебе звертатися? Введи ім'я та прізвище.",
        FormStep.Company => "Вкажи назву компанії.",
        FormStep.Industry => "Чим займається твоя компанія або бізнес? Обери варіант:",
        FormStep.BusinessDetails => "Опиши свою діяльність у сфері e-commerce.",
        FormStep.ProductionDetails => "Якою саме продукцією займається твоє виробництво?",
        FormStep.Telegram => "Можеш поділитися своїм контактом кнопкою нижче або ввести Telegram вручну.",
        FormStep.Instagram => "Вкажи свій Instagram.",
        FormStep.Phone => "Вкажи номер телефону або поділися контактом кнопкою нижче.",
        _ => "Введи відповідь."
    };

    KeyboardButton[][] buttons;

    if (field == FormStep.Services)
    {
        buttons = new[]
        {
            new KeyboardButton[] { "📦 Повний пакет BTshop" },
            new KeyboardButton[] { "🚢 Імпорт товарів" },
            new KeyboardButton[] { "🛒 Супровід e-commerce" },
            new KeyboardButton[] { "➕ Інша послуга" },
            new KeyboardButton[] { "✅ Готово", "⏭ Пропустити" }
        };
    }

    else if (field == FormStep.Industry)
    {
        buttons = new[]
        {
        new KeyboardButton[] { "🏭 Виробництво" },
        new KeyboardButton[] { "🛒 Продажі в інтернеті" },
        new KeyboardButton[] { "📦 Оптові продажі" },
        new KeyboardButton[] { "🏪 Роздрібні продажі" },
        new KeyboardButton[] { "✏️ Інше" },
        new KeyboardButton[] { "⏭ Пропустити" },
        new KeyboardButton[] { "❓ Допомога" }
    };
    }

    else if (field is FormStep.Telegram or FormStep.Phone)
    {
        buttons = new[]
        {
            new KeyboardButton[] { KeyboardButton.WithRequestContact("📲 Поділитися контактом") },
            new KeyboardButton[] { "⏭ Пропустити" },
            new KeyboardButton[] { "❓ Допомога" }
        };
    }
    else
    {
        buttons = new[]
        {
            new KeyboardButton[] { "⏭ Пропустити" },
            new KeyboardButton[] { "❓ Допомога" }
        };
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
        $"Деталі діяльності: {Display(form.BusinessDetails)}\n" +
        $"Деталі виробництва: {Display(form.ProductionDetails)}\n" +
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
        case FormStep.BusinessDetails:
            form.BusinessDetails = value;
            break;

        case FormStep.ProductionDetails:
            form.ProductionDetails = value;
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
    Services,
    Name,
    Company,
    Industry,
    BusinessDetails,
    ProductionDetails,
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
    public List<string> SelectedServices { get; set; } = new();
    public string BusinessDetails { get; set; } = "";
    public string ProductionDetails { get; set; } = "";
    public string Telegram { get; set; } = "";
    public string Instagram { get; set; } = "";
    public string Phone { get; set; } = "";
}


class AdminClientForm
{
    public string Name { get; set; } = "";
    public string Company { get; set; } = "";
    public string Industry { get; set; } = "";
    public List<string> SelectedServices { get; set; } = new();
    public bool IsSelectingServices { get; set; } = false;
    public string BusinessDetails { get; set; } = "";
    public string ProductionDetails { get; set; } = "";
    public string Telegram { get; set; } = "";
    public string Instagram { get; set; } = "";
    public string Phone { get; set; } = "";
}

