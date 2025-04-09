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

            var bot = new MeanReversionBot(
                apiKey: "ТВОЙ_API_KEY",
                apiSecret: "ТВОЙ_SECRET_KEY",
                symbol: "SOLUSDT"               // или BTCUSDT, ETHUSDT и т.д.
            );

            await bot.StartLoopAsync();
        }

        public class MeanReversionBot
        {
            //параметры:

            private string symbol = "";

            

            private readonly BybitRestClient _client;



            private decimal walletBalance = 20m;
            private decimal assetAmount = 0m;
            private decimal? lastBuyPrice = null;
            private decimal _lastPrice = 0m;

            public MeanReversionBot(string apiKey, string apiSecret)
            {
                ReadConfig();

        


                _client = new BybitRestClient(options =>
                {
                    options.ApiCredentials = new ApiCredentials(apiKey, apiSecret);
                });
            }

            private void ReadConfig()
            {
                //считать из конфигурации потом:
                symbol = "SOLUSDT";
            }

            public async Task StartLoopAsync()
            {
                Console.WriteLine("Mean Reversion Bot (тестовый режим) запущен...\n");

                while (true)
                {
                    try
                    {

                        _lastPrice = await GetCurrentPriceAsync();

                        var (ema20, ema50) = await GetEmaValuesAsync();

                        if (lastBuyPrice == null) Console.WriteLine($"[{DateTime.Now:T}] | Цена: {_lastPrice:F2} > EMA20: {ema20:F2} > EMA50: {ema50:F2} | Баланс =  USDT: {walletBalance:F2}, SOL: {assetAmount:F5}");
                        else Console.WriteLine($"[{DateTime.Now:T}] | Цена покупки: {lastBuyPrice:F2} < Цена: {_lastPrice:F2} < EMA20: {ema20:F2} < EMA50: {ema50:F2} | Баланс =  USDT: {walletBalance:F2}, SOL: {assetAmount:F5}");


                        if (lastBuyPrice == null)
                        {
                            if (ema20 > ema50 && _lastPrice > ema20)
                            {
                                await SimulateBuy();
                            }
                        }
                        else
                        {
                            if (_lastPrice > lastBuyPrice && ema20 < ema50 && _lastPrice < ema20)
                            {
                                await SimulateSell();
                            }
                        }

                        await Task.Delay(TimeSpan.FromSeconds(10));


                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Ошибка: {ex.Message}");
                        await Task.Delay(TimeSpan.FromSeconds(30));
                    }
                }
            }


            private async Task<decimal> GetCurrentPriceAsync()
            {
                var ticker = await _client.V5Api.ExchangeData.GetSpotTickersAsync(_symbol);
                if (!ticker.Success || ticker.Data == null)
                    throw new Exception($"Ошибка получения цены: {ticker.Error}");

                var item = ticker.Data.List.FirstOrDefault();
                if (item == null || item.LastPrice == 0)
                    throw new Exception("Цена отсутствует");

                return item.LastPrice;
            }


            private Task SimulateBuy()
            {
                decimal quantityToBuy = Math.Round(walletBalance / _lastPrice, 6);
                decimal cost = quantityToBuy * _lastPrice;

                walletBalance -= cost;
                assetAmount += quantityToBuy;
                lastBuyPrice = _lastPrice;

                Console.WriteLine($"[{DateTime.Now:T}] | Покупка: {quantityToBuy} {_symbol} по {_lastPrice:F2} USDT");
                Console.WriteLine($"Баланс: {walletBalance:F2} USDT | Активов: {assetAmount} {_symbol}");

                return Task.CompletedTask;
            }

            private Task SimulateSell()
            {
                decimal revenue = assetAmount * _lastPrice;
                walletBalance += revenue;

                Console.WriteLine($"[{DateTime.Now:T}] | Продажа: {assetAmount} {_symbol} по {_lastPrice:F2} USDT");
                Console.WriteLine($"Баланс после продажи: {walletBalance:F2} USDT");

                assetAmount = 0;
                lastBuyPrice = null;

                return Task.CompletedTask;
            }



            public async Task<(decimal ema20, decimal ema50)> GetEmaValuesAsync()
            {
                var candles = await GetLastCandlesAsync(); // получи 100+ свечей с ценами
                var quotes = candles.Select(c => new Quote
                {
                    Date = c.DT,
                    Open = c.Open,
                    High = c.High,
                    Low = c.Low,
                    Close = c.Close,
                    Volume = 0
                }).ToList();

                var ema20 = quotes.GetEma(20).LastOrDefault()?.Ema;
                var ema50 = quotes.GetEma(50).LastOrDefault()?.Ema;

                if (ema20 == null || ema50 == null)
                    throw new Exception("Недостаточно данных для EMA");
                return ((decimal)ema20, (decimal)ema50);
            }

            public async Task<List<Candle>> GetLastCandlesAsync(int limit = 200)
            {
                var result = await _client.V5Api.ExchangeData.GetKlinesAsync(
                    category: Category.Spot,
                    symbol: _symbol,
                    interval: KlineInterval.OneMinute,
                    limit: limit
                );

                if (!result.Success || result.Data?.List == null)
                    throw new Exception($"❌ Ошибка получения свечей: {result.Error}");

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
            public decimal Open { get; set; }
            public decimal High { get; set; }
            public decimal Low { get; set; }
            public decimal Close { get; set; }
            public DateTime DT { get; set; }

        }
    }
}
