using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Bot.Types;
using System.Threading;

namespace ByBitBot_AF
{
    class TelegrammBot
    {
        //параметры из строки запуска контейнера Docker:
        private string docker_telegrammToken = Environment.GetEnvironmentVariable("telegrammtoken") ?? "";

        private readonly TelegramBotClient bot;
        private ChatId chatId = 0;
        public string lastLoggingMessage { get; set; }

        public TelegrammBot()
        {
            if (docker_telegrammToken.Trim() != "")
            {
                bot = new TelegramBotClient(docker_telegrammToken);

                Start();
            }
        }

        private async void Start()
        {
            var me = await bot.GetMe();

            Console.WriteLine($"Телеграмм бот {me.Username} успешно запущен запущен!\n");

            bot.OnMessage += OnMessage;
        }
        private async Task OnMessage(Message msg, UpdateType type)
        {
            if (msg == null || msg.Text == null) return;

            if (msg.Text.ToUpper() == "GO")
            {
                chatId = msg.Chat.Id;

                var replyKeyboard = new ReplyKeyboardMarkup(new[]
                {
                    new KeyboardButton[] { "Статус", "Баланс", "Тест" }
                })
                {
                    ResizeKeyboard = true // уменьшает размер под экран
                };

                await bot.SendMessage(
                    chatId: chatId,
                    text: "chatId успешно считан!",
                    replyMarkup: replyKeyboard
                );
            }

            if (msg.Text == "Статус")
            {
       
            }
            if (msg.Text == "Баланс")
            {

            }
            if (msg.Text == "Тест")
            {
                if (chatId != 0)
                {
                    await bot.SendMessage(chatId, lastLoggingMessage);
                }
            }
        }

        public async void TgBotSendMessage(string message)
        {
            if (chatId != 0)
            {
                await bot.SendMessage(chatId, message);
            }
        }
    }
}
