using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Bot.Types;

namespace ByBitBot_AF
{
    class TelegrammBot
    {
        private readonly TelegramBotClient bot;
        private readonly string token = "7766788849:AAEpUNuaAdkLWEbRd__8jvgrj66FA9P1dPg";
        private ChatId chatId = 0;

        public TelegrammBot()
        {
            bot = new TelegramBotClient(token);

            Start();
        }

        private async void Start()
        {
            var me = await bot.GetMe();

            Console.WriteLine($"Телеграмм бот {me.Username} успешно запущен запущен!\n");

            bot.OnMessage += OnMessage;
        }
        private async Task OnMessage(Message msg, UpdateType type)
        {
            if (msg.Text == "GO")
            {
                chatId = msg.Chat.Id;

                await bot.SendMessage(chatId, "chatId успешно считан!");
            }

            if (msg.Text == "11")
            {
                //await bot.SendMessage(msg.Chat, "Welcome! Pick one direction",
                //    replyMarkup: new InlineKeyboardButton[] { "Left", "Right" });
            }
            if (msg.Text == "12")
            {
                //await bot.SendMessage(msg.Chat, "Welcome! Pick one direction",
                //    replyMarkup: new InlineKeyboardButton[] { "Left", "Right" });
            }
            if (msg.Text == "13")
            {
                //await bot.SendMessage(msg.Chat, "Welcome! Pick one direction",
                //    replyMarkup: new InlineKeyboardButton[] { "Left", "Right" });
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
