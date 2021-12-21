using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Extensions.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

Dictionary<long, UserInfo> users = new Dictionary<long, UserInfo>();
var botClient = new TelegramBotClient("");

using var cts = new CancellationTokenSource();

// StartReceiving does not block the caller thread. Receiving is done on the ThreadPool.
var receiverOptions = new ReceiverOptions
{
    AllowedUpdates = { } // receive all update types
};
botClient.StartReceiving(
    HandleUpdateAsync,
    HandleErrorAsync,
    receiverOptions,
    cancellationToken: cts.Token);

var me = await botClient.GetMeAsync();

Console.WriteLine($"Start listening for @{me.Username}");
Console.ReadLine();

// Send cancellation request to stop bot
cts.Cancel();

async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
{
    // Only process Message updates: https://core.telegram.org/bots/api#message
    if (update.Type != UpdateType.Message)
        return;
    // Only process text messages
    if (update.Message!.Type != MessageType.Text)
        return;

    var chatId = update.Message.Chat.Id;
    var messageText = update.Message.Text;

    long id = update.Message.From.Id;
    UserInfo empty;
    if (!users.TryGetValue(id, out empty))
    {
        users.Add(id, null);
    }

    await System.IO.File.WriteAllTextAsync("BotHistory.txt", messageText + "   " + update.Message.From);
    Console.WriteLine(messageText + "   " + update.Message.From);

    UserInfo user = users[id];
    string answer = "";
    if (messageText.Split(' ')[0] == "/start")
    {
        Random random = new Random();
        string randomAnswer = "";
        List<int> values = new List<int>();
        for (int i = 0; i < 4; i++)
        {
            int value;
            do
            {
                value = random.Next(0, 9);
            } while (values.Contains(value));
            values.Add(value);

            randomAnswer += value;
        }
        users[id] = new UserInfo() { End = false, Tries = 15, Number = randomAnswer };
        answer = "Hello, my name is Tomik.\nI am here to let you play the Amazing Game of Cows and Bulls.\n" +
        "You have to guess 4-digit number. If the matching digits are in their right positions, they are \"bulls\", if in different positions, they are \"cows\".\n You have 15 tries. Good Luck!\n" +
        "UPDATE\nSame digits can't appear in the number.";
    }
    else
    {
        if (user == null || user.End)
        {
            answer = "PLease type /start to start the game.";
        }
        else
        {
            if (CheckSameValues(messageText))
            {
                answer = "Same digits can't appear in the number. Try again.";
            }
            else
                answer = AnalizeNumber(update.Message);
        }
    }

    Message sentMessage = await botClient.SendTextMessageAsync(
        chatId: chatId,
        text: answer,
        cancellationToken: cancellationToken);
}

Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
{
    var ErrorMessage = exception switch
    {
        ApiRequestException apiRequestException
            => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
        _ => exception.ToString()
    };

    Console.WriteLine(ErrorMessage);
    return Task.CompletedTask;
}

string AnalizeNumber(Message message)
{
    string guess = message.Text;

    if (guess.Length != 4 || !IsDigitsOnly(guess))
    {
        return "Answer format is wrong.\n" +
            "Please use correct format: 1234";
    }
    long id = message.From.Id;
    string answer = users[id].Number;
    int cows = 0;
    int bulls = 0;

    for (int index = 0, a = 0; a < users[id].Number.Length; index++, a++)
    {
        if (guess[index] == answer[index])
        {
            bulls++;
            guess = guess.Remove(index, 1);
            answer = answer.Remove(index, 1);
            index--;
        }
    }

    for (int index = 0; index < guess.Length; index++)
    {
        if (answer.Contains(guess[index].ToString()))
        {
            cows++;
            answer = answer.Remove(answer.IndexOf(guess[index].ToString()), 1);
            guess = guess.Remove(index, 1);
            index--;
        }
    }

    if (bulls != 4)
    {
        int value = users[id].Tries;
        users[id] = new UserInfo() { End = users[id].Tries <= 0, Tries = value - 1, Number = users[id].Number };
        if (users[id].Tries <= 0)
        {
            return "Sorry, you're out of tries. Please type /start to try again.";
        }
        else
            return $"Bulls: {bulls} Cows: {cows}.    Tries left: {users[id].Tries}";
    }
    else
    {
        users[id] = new UserInfo() { End = true };
        return "Congratulations you won!";
    }
}

bool IsDigitsOnly(string str)
{
    foreach (char c in str)
    {
        if (c < '0' || c > '9')
            return false;
    }

    return true;
}

bool CheckSameValues(string text)
{
    for (int i = 0; i < 4; i++)
    {
        if (text.IndexOf(text[i]) != text.LastIndexOf(text[i]))
            return true;
    }
    return false;
}
class UserInfo
{
    public bool End;
    public int Tries;
    public string Number;
}
