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
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection;

namespace ByBitBot_AF
{
    public class MeanReversionBot
    {
        //параметры из строки запуска контейнера Docker:
        private string docker_apiKey = Environment.GetEnvironmentVariable("apikey") ?? "";
        private string docker_apiSecret = Environment.GetEnvironmentVariable("apisecret") ?? "";
        private string docker_coin = Environment.GetEnvironmentVariable("coin") ?? "";
        private string docker_currency = Environment.GetEnvironmentVariable("currency") ?? "";
        private string docker_emaFastLength = Environment.GetEnvironmentVariable("emafastlength") ?? "";
        private string docker_emaSlowLength = Environment.GetEnvironmentVariable("emaslowlength") ?? "";
        private string docker_candleInterval = Environment.GetEnvironmentVariable("candleInterval") ?? "";
        private string docker_cycle = Environment.GetEnvironmentVariable("cycle") ?? "";

        //параметры из конфигурации:     
        private string apiKey;                                  //ключ API KEY из ByBIT
        private string apiSecret;                               //ключ API SECRET из ByBIT
        private string coin;                                    //монета
        private string currency;                                //валюта
        private string symbol;                                  //торговая пара
        private int emaFastLength;                              //длина экспоненциальной скользящей средней на короткий период
        private int emaSlowLength;                              //длина экспоненциальной скользящей средней на длинный период
        private KlineInterval candleInterval;                   //интервал свечей
        private TimeSpan cycle;                                 //таймер работы (цикл работы)

        //свойства
        private readonly GetCoinData _getCoinData;
        private readonly GetWalletData _getWalletData;
        private readonly BybitRestClient _client;
        private readonly TelegrammBot _telegrammBot;
        private decimal? lastBuyPrice { get; set; } = null;           //цена последней покупки

        //свойства монеты
        private decimal lastPrice { get; set; }             //последняя цена
        private decimal emaFast { get; set; }               //величина экспоненциальной скользящей средней на короткий период    
        private decimal emaSlow { get; set; }               //величина экспоненциальной скользящей средней на длинный период


        public MeanReversionBot()
        {
            ReadConfig();

            _client = new BybitRestClient(options =>
            {
                options.ApiCredentials = new ApiCredentials(apiKey, apiSecret);
            });

            _getCoinData = new GetCoinData(_client, symbol, candleInterval);
            _getWalletData = new GetWalletData(_client, coin);
            _telegrammBot = new TelegrammBot();
        }

        private void ReadConfig()
        {
            apiKey = docker_apiKey;
            apiSecret = docker_apiSecret;

            coin = docker_coin;
            currency = docker_currency;

            symbol = coin + currency;

            emaFastLength = Convert.ToInt32(docker_emaFastLength);
            emaSlowLength = Convert.ToInt32(docker_emaSlowLength);

            string _candleInterval = docker_candleInterval;
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

            cycle = TimeSpan.FromSeconds(Convert.ToInt32(docker_cycle));
        }

        public async Task StartLoopAsync()
        {
            Console.WriteLine("Mean Reversion Bot запущен.....\n");

            while (true)
            {
                try
                {
                    await _getWalletData.UpdateWalletData();

                    //получение текущей цены SOL/USDT
                    lastPrice = await _getCoinData.GetCurrentPriceAsync(symbol);

                    //получение текущих значений EMA
                    var (emaFastResult, emaSlowResult) = await _getCoinData.GetEmaValuesAsync(emaFastLength, emaSlowLength);
                    emaFast = emaFastResult;
                    emaSlow = emaSlowResult;

                    string w1 = "";
                    if (lastPrice > emaFast) w1 = ">"; else w1 = "<";
                    string w2 = "";
                    if (emaFast > emaSlow) w2 = ">"; else w2 = "<";
                    
                    if (lastBuyPrice == null)
                    {
                        Console.WriteLine($"[{DateTime.Now:T}] | {symbol} | Ожидание покупки.. | Цена:{lastPrice:F2} {w1} ema{emaFastLength}:{emaFast:F2} {w2} ema{emaSlowLength}:{emaSlow:F2} | USDT:{_getWalletData.TotalAvailableBalance:F3}, SOL:{_getWalletData.AssetBalance:F5}, Wallet:{_getWalletData.TotalEquity:F3}");

                        _telegrammBot.lastLoggingMessage = $"{DateTime.Now:T}\n{symbol}\nОжидание покупки..\nЦена:{lastPrice:F2}\nema{emaFastLength}:{emaFast:F2}\nema{emaSlowLength}:{emaSlow:F2}";
                    }
                    else
                    {
                        decimal priceChange = ((decimal)lastPrice * 100 / (decimal)lastBuyPrice) - 100;
                        string w3 = "";
                        if (lastBuyPrice > lastPrice) w3 = ">"; else w3 = "<";

                        Console.WriteLine($"[{DateTime.Now:T}] | {symbol} | Ожидание продажи.. | {priceChange:F2}% | Цена покупки:{lastBuyPrice} {w3} Цена:{lastPrice:F2} {w1} ema{emaFastLength}:{emaFast:F2} {w2} ema{emaSlowLength}:{emaSlow:F2} | USDT:{_getWalletData.TotalAvailableBalance:F3}, SOL:{_getWalletData.AssetBalance:F5}, Wallet:{_getWalletData.TotalEquity:F3}");

                        _telegrammBot.lastLoggingMessage = $"{DateTime.Now:T}\n{symbol}\nОжидание продажи..\nИзменение цены:{priceChange:F2}%\nЦена покупки:{lastBuyPrice}\nЦена:{lastPrice:F2}\nema{emaFastLength}:{emaFast:F2}\nema{emaSlowLength}:{emaSlow:F2}";
                    }

                    if (lastBuyPrice == null)
                    {
                        if (lastPrice > emaFast && emaFast > emaSlow)
                        {
                            await Buy();
                        }
                    }
                    else
                    {
                        if (lastBuyPrice < lastPrice && lastPrice < emaFast && emaFast < emaSlow)
                        {
                            await Sell();
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
        /// Покупка
        /// </summary>
        private async Task Buy()
        {
            try
            {
                var usdtBalance = Math.Round(((decimal)_getWalletData.TotalAvailableBalance * 0.98m), 2);     // 98% от всех доступных средств
                var minOrderValue = _getWalletData.MinOrderBuyValue;

                if (usdtBalance < minOrderValue)
                {
                    Console.WriteLine($"[{DateTime.Now:T}] | Недостаточно USDT: {usdtBalance:F3} (нужно хотя бы {minOrderValue} USDT)");
                    return;
                }              

                var result = await _client.V5Api.Trading.PlaceOrderAsync(
                        Bybit.Net.Enums.Category.Spot,                          // Спотовая торговля
                        symbol,                                                 // Пара
                        Bybit.Net.Enums.OrderSide.Buy,                          // Сторона сделки
                        Bybit.Net.Enums.NewOrderType.Market,                    // Тип ордера: рыночный
                        usdtBalance,                                                   // Кол-во
                        marketUnit: MarketUnit.QuoteAsset
                        );

                if (result.Success)
                {
                    Console.WriteLine($"[{DateTime.Now:T}] | Покупка {symbol} на сумму: {usdtBalance} USDT");
                    _telegrammBot.TgBotSendMessage($"Покупка {symbol} на сумму: {usdtBalance} USDT");

                    lastBuyPrice = lastPrice;
                }
                else
                {
                    Console.WriteLine($"Ошибка ордера: {result.Error}");
                    _telegrammBot.TgBotSendMessage($"Ошибка ордера: {result.Error}");
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now:T}] | Exception: {ex.Message}");
            }
        }

        /// <summary>
        /// Продажа 
        /// </summary>
        private async Task Sell()
        {
            try
            {
                // Достаём баланс актива (например, SOL)
                var assetBalance = _getWalletData.AssetBalance;

                // Достаём минимально допустимое количество для продажи
                var minOrderQuantity = _getWalletData.MinOrderSellValue;

                // Оставляем небольшой запас и округляем
                decimal quantityToSell = Math.Round((decimal)assetBalance * 0.98m, 3); // 98%, 0.001 precision

                if (quantityToSell < minOrderQuantity)
                {
                    Console.WriteLine($"[{DateTime.Now:T}] | Недостаточно {symbol} для продажи: {quantityToSell:F3} (минимум {minOrderQuantity})");
                    return;
                }

                // Отправляем маркет-ордер на продажу
                var result = await _client.V5Api.Trading.PlaceOrderAsync(
                    Bybit.Net.Enums.Category.Spot,
                    symbol,
                    Bybit.Net.Enums.OrderSide.Sell,
                    Bybit.Net.Enums.NewOrderType.Market,
                    quantityToSell
                );

                if (result.Success)
                {
                    Console.WriteLine($"[{DateTime.Now:T}] | Продажа {symbol}: {quantityToSell}");
                    _telegrammBot.TgBotSendMessage($"Продажа {symbol}: {quantityToSell}");

                    lastBuyPrice = null;
                }
                else
                {
                    Console.WriteLine($"Ошибка ордера: {result.Error}");
                    _telegrammBot.TgBotSendMessage($"Ошибка ордера: {result.Error}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now:T}] | Exception: {ex.Message}");
            }
        }




    }
}
