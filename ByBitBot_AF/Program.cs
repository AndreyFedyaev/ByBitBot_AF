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

namespace ByBit_TestBot
{
    class Program
    {
        static async Task Main(string[] args)
        {

            var bot = new MeanReversionBot();

            await bot.StartLoopAsync();
        }

        public class MeanReversionBot
        {
            //параметры из конфигурации:
            private bool mode = false;          //режим работы: false - симуляция, true - реальная торговля         
            private string apiKey = "";         //ключ API KEY из ByBIT
            private string apiSecret = "";      //ключ API SECRET из ByBIT
            private string symbol = "";         //торговая пара
            private int maxBuyAmount = 1;       //максимум средств на покупку
            private int emaFastLength = 20;           //длина экспоненциальной скользящей средней на короткий период
            private int emaSlowLength = 50;           //длина экспоненциальной скользящей средней на длинный период
            private KlineInterval candleInterval = KlineInterval.OneMinute;         //интервал свечей
            private TimeSpan cycle = TimeSpan.FromSeconds(60);                       //таймер работы

            //свойства
            private readonly BybitRestClient _client;
            private decimal walletBalance { get; set; }     //общий баланс кошелька
            private decimal usdtBalance { get; set; }       //количество USDT в кошельке
            private decimal assetBalance { get; set; }      //количество монет в кошельке
            private decimal lastPrice { get; set; }         //последняя цена
            private decimal emaFast { get; set; }           //величина экспоненциальной скользящей средней на короткий период    
            private decimal emaSlow { get; set; }           //величина экспоненциальной скользящей средней на длинный период
            private decimal? lastBuyPrice { get; set; } = null;           //цена последней покупки


            public MeanReversionBot()
            {
                ReadConfig();

                //для симуляции
                if (!mode)
                {
                    usdtBalance = 20m;
                    assetBalance = 0m;
                }

                _client = new BybitRestClient(options =>
                {
                    options.ApiCredentials = new ApiCredentials(apiKey, apiSecret);
                });
            }

            private void ReadConfig()
            {
                //считать из конфигурации потом:
                mode = false;
                apiKey = "ТВОЙ_API_KEY";
                apiSecret = "ТВОЙ_SECRET_KEY";
                symbol = "SOLUSDT";
                maxBuyAmount = 10;
                emaFastLength = 20;
                emaSlowLength = 50;
                string _candleInterval = "OneMinute";
                int _cycle = 10;



                switch (_candleInterval)
                {
                    case "OneMinute":
                        candleInterval = KlineInterval.OneMinute;
                        break;
                    case "ThreeMinutes":
                        candleInterval = KlineInterval.ThreeMinutes;
                        break;
                    case "FiveMinutes":
                        candleInterval = KlineInterval.FiveMinutes;
                        break;
                    case "FifteenMinutes":
                        candleInterval = KlineInterval.FifteenMinutes;
                        break;
                    case "ThirtyMinutes":
                        candleInterval = KlineInterval.ThirtyMinutes;
                        break;
                    case "OneHour":
                        candleInterval = KlineInterval.OneHour;
                        break;
                    case "TwoHours":
                        candleInterval = KlineInterval.TwoHours;
                        break;
                    case "FourHours":
                        candleInterval = KlineInterval.FourHours;
                        break;
                    case "SixHours":
                        candleInterval = KlineInterval.SixHours;
                        break;
                    case "TwelveHours":
                        candleInterval = KlineInterval.TwelveHours;
                        break;
                    case "OneDay":
                        candleInterval = KlineInterval.OneDay;
                        break;
                    case "OneWeek":
                        candleInterval = KlineInterval.OneWeek;
                        break;
                    case "OneMonth":
                        candleInterval = KlineInterval.OneMonth;
                        break;
                    default:
                        candleInterval = KlineInterval.OneMinute;
                        break;
                }

                cycle = TimeSpan.FromSeconds(_cycle);
            }

            public async Task StartLoopAsync()
            {
                if (mode) Console.WriteLine("Mean Reversion Bot запущен в режиме Online...\n");
                else Console.WriteLine("Mean Reversion Bot запущен в режиме Симуляции...\n");

                while (true)
                {
                    try
                    {
                        //текущая цена
                        lastPrice = await GetCurrentPriceAsync(symbol);

                        //получение ema
                        var (emaFastResult, emaSlowResult) = await GetEmaValuesAsync();
                        emaFast = emaFastResult;
                        emaSlow = emaSlowResult;

                        if(!mode) walletBalance = (assetBalance * lastPrice) + usdtBalance;

                        if (lastBuyPrice == null) Console.WriteLine($"[{DateTime.Now:T}] | Цена:{lastPrice:F2} > EMA{emaFastLength}:{emaFast:F2} > EMA{emaSlowLength}:{emaSlow:F2} | Баланс =  USDT:{usdtBalance:F2}, SOL:{assetBalance:F5}, Wallet:{walletBalance:F3}");
                        else Console.WriteLine($"[{DateTime.Now:T}] | Цена покупки:{lastBuyPrice:F2} < Цена:{lastPrice:F2} < EMA{emaFastLength}:{emaFast:F2} < EMA{emaSlowLength}:{emaSlow:F2} | Баланс =  USDT:{usdtBalance:F2}, SOL:{assetBalance:F5}, Wallet:{walletBalance:F3}");


                        if (lastBuyPrice == null)
                        {
                            if (lastPrice > emaFast && emaFast > emaSlow)
                            {
                                if(!mode) await SimulateBuy();
                            }
                        }
                        else
                        {
                            if (lastBuyPrice < lastPrice && lastPrice < emaFast && emaFast < emaSlow)
                            {
                                if (!mode) await SimulateSell();
                            }
                        }

                        await Task.Delay(cycle);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Ошибка: {ex.Message}");
                        await Task.Delay(cycle);
                    }
                }
            }

            /// <summary>
            /// Получает текущую цену указанной торговой пары
            /// </summary>
            /// <param name="readSymbol">торговая пара для получения текущей цены</param>
            private async Task<decimal> GetCurrentPriceAsync(string readSymbol)
            {
                var ticker = await _client.V5Api.ExchangeData.GetSpotTickersAsync(readSymbol);
                if (!ticker.Success || ticker.Data == null)
                    throw new Exception($"Ошибка получения цены {readSymbol}: {ticker.Error}");

                var item = ticker.Data.List.FirstOrDefault();
                if (item == null || item.LastPrice == 0)
                    throw new Exception("Цена отсутствует");

                return item.LastPrice;
            }

            /// <summary>
            /// Покупка в режиме симуляции
            /// </summary>
            private Task SimulateBuy()
            {
                decimal quantityToBuy = Math.Round(usdtBalance / lastPrice, 6);
                decimal cost = quantityToBuy * lastPrice;

                usdtBalance -= cost;
                assetBalance += quantityToBuy;
                lastBuyPrice = lastPrice;
                walletBalance = (assetBalance * lastPrice) + usdtBalance;

                Console.WriteLine($"[{DateTime.Now:T}] | Покупка {symbol}: {quantityToBuy} по {lastPrice:F2} USDT");
                Console.WriteLine($"[{DateTime.Now:T}] | Баланс USDT:{usdtBalance:F2} | Активов {symbol}:{assetBalance} | Баланс кошелька: {walletBalance}");

                return Task.CompletedTask;
            }

            /// <summary>
            /// Продажа в режиме симуляции
            /// </summary>
            private Task SimulateSell()
            {
                decimal revenue = assetBalance * lastPrice;
                usdtBalance += revenue;
                walletBalance = (assetBalance * lastPrice) + usdtBalance;

                Console.WriteLine($"[{DateTime.Now:T}] | Продажа {symbol}: {assetBalance} по {lastPrice:F2} USDT");
                Console.WriteLine($"[{DateTime.Now:T}] | Баланс USDT:{usdtBalance:F2} | Активов {symbol}:{assetBalance} | Баланс кошелька: {walletBalance}");

                assetBalance = 0;
                lastBuyPrice = null;

                return Task.CompletedTask;
            }

            /// <summary>
            /// Рассчитывает значения короткой и длинной экспоненциальной скользящей средней
            /// </summary>
            public async Task<(decimal resultEmaFast, decimal resultEmaSlow)> GetEmaValuesAsync()
            {
                var candles = await GetLastCandlesAsync(200);      //получаем информацию о 200 последних свечах
                var quotes = candles.Select(c => new Quote
                {
                    Date = c.DT,
                    Open = c.Open,
                    High = c.High,
                    Low = c.Low,
                    Close = c.Close,
                    Volume = 0
                }).ToList();

                var emaFastData = quotes.GetEma(emaFastLength).LastOrDefault()?.Ema;
                var emaSlowData = quotes.GetEma(emaSlowLength).LastOrDefault()?.Ema;

                if (emaFastData == null || emaSlowData == null)
                    throw new Exception("Недостаточно данных для рассчета значений EMA");
                return ((decimal)emaFastData, (decimal)emaSlowData);
            }

            /// <summary>
            /// Получает список свечей
            /// </summary>
            /// <param name="count">количество свечей</param>
            public async Task<List<Candle>> GetLastCandlesAsync(int count)
            {
                var result = await _client.V5Api.ExchangeData.GetKlinesAsync(
                    category: Category.Spot,
                    symbol: symbol,
                    interval: candleInterval,
                    limit: count
                );

                if (!result.Success || result.Data?.List == null)
                    throw new Exception($"Ошибка получения свечей: {result.Error}");

                return result.Data.List
                    .Select(k => new Candle
                    {
                        DT = k.StartTime,
                        Open = k.OpenPrice,
                        High = k.HighPrice,
                        Low = k.LowPrice,
                        Close = k.ClosePrice
                    })
                    .ToList();
            }
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
