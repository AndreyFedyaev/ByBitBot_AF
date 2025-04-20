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
        private string docker_telegrammChatID = Environment.GetEnvironmentVariable("telegrammchatid") ?? "";

        private readonly TelegramBotClient bot;
        private ChatId chatId = 0;
        public string info { get; set; }
        public string ordersInfo { get; set; }
        public string lastBalanceInfo { get; set; }
        public string gridInfo { get; set; }
       

        public TelegrammBot()
        {
            if (docker_telegrammToken.Trim() != "")
            {
                bot = new TelegramBotClient(docker_telegrammToken);
                if(docker_telegrammChatID.Trim() != "") chatId = Convert.ToInt32(docker_telegrammChatID);

                Start();
            }
        }

        private async void Start()
        {
            var me = await bot.GetMe();

            Console.WriteLine($"Телеграмм бот {me.Username} успешно запущен запущен!\n");

            var replyKeyboard = new ReplyKeyboardMarkup(new[]
            {
                new KeyboardButton[] { "Инфо", "Баланс", "Ордеры", "Сетка" }
            })
            {
                ResizeKeyboard = true // уменьшает размер под экран
            };

            await bot.SendMessage(
                chatId: chatId,
                text: "telegrammBot запущен!",
                replyMarkup: replyKeyboard
            );

            bot.OnMessage += OnMessage;
        }
        private async Task OnMessage(Message msg, UpdateType type)
        {
            if (msg == null || msg.Text == null) return;

            if (msg.Text == "Инфо")
            {
                if (chatId != 0)
                {
                    await bot.SendMessage(chatId, info);
                }
            }
            if (msg.Text == "Баланс")
            {
                if (chatId != 0)
                {
                    await bot.SendMessage(chatId, lastBalanceInfo);
                }
            }
            if (msg.Text == "Ордеры")
            {
                if (chatId != 0)
                {
                    await bot.SendMessage(chatId, ordersInfo);
                }
            }
            if (msg.Text == "Сетка")
            {
                if (chatId != 0)
                {
                    await bot.SendMessage(chatId, gridInfo);
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
