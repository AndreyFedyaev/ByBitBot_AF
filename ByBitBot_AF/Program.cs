using System;
using System.Threading.Tasks;
using Bybit.Net.Clients;
using Bybit.Net.Objects;
using Bybit.Net.Objects.Models.V5;
using CryptoExchange.Net.Authentication;
using CryptoExchange.Net.Interfaces;
using System.Linq;
using System.Threading;
using Bybit.Net.Enums;
using Skender.Stock.Indicators;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using ByBitBot_AF;

namespace ByBit_TestBot
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var bot = new MeanReversionBot();

            await bot.StartLoopAsync();
        }

        
        public class Candle
        {
            /// <summary>
            /// Цена открытия
            /// </summary>
            public decimal Open { get; set; }
            /// <summary>
            /// Максимальная цена
            /// </summary>
            public decimal High { get; set; }
            /// <summary>
            /// Минимальная цена
            /// </summary>
            public decimal Low { get; set; }
            /// <summary>
            /// Цена закрытия
            /// </summary>
            public decimal Close { get; set; }
            /// <summary>
            /// дата/время
            /// </summary>
            public DateTime DT { get; set; }
        }
    }
}
