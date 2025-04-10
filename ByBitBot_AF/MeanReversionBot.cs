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

namespace ByBitBot_AF
{
    public class MeanReversionBot
    {
        //параметры из конфигурации:     
        private string apiKey = "";                 //ключ API KEY из ByBIT
        private string apiSecret = "";              //ключ API SECRET из ByBIT
        private string coin = "";
        private string currency = "";
        private string symbol = "";          //торговая пара
        private int emaFastLength = 20;             //длина экспоненциальной скользящей средней на короткий период
        private int emaSlowLength = 50;             //длина экспоненциальной скользящей средней на длинный период
        private KlineInterval candleInterval;         //интервал свечей
        private TimeSpan cycle = TimeSpan.FromSeconds(10);                      //таймер работы

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
            //считать из конфигурации потом:
            apiKey = "7ogJDgTTyskzzDRYYN";                              //ТВОЙ_API_KEY
            apiSecret = "SOAOwWXnseX4KQq19WMIFBrYA9VB8iptfMO2";         //ТВОЙ_SECRET_KEY
            coin = "SOL";
            currency = "USDT";
            string _candleInterval = "ThreeMinutes";

            symbol = coin + currency;
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

                        Console.WriteLine($"[{DateTime.Now:T}] | {symbol} | Ожидание продажи.. | {priceChange}% | Цена покупки:{lastBuyPrice} {w3} Цена:{lastPrice:F2} {w1} ema{emaFastLength}:{emaFast:F2} {w2} ema{emaSlowLength}:{emaSlow:F2} | USDT:{_getWalletData.TotalAvailableBalance:F3}, SOL:{_getWalletData.AssetBalance:F5}, Wallet:{_getWalletData.TotalEquity:F3}");

                        _telegrammBot.lastLoggingMessage = $"{DateTime.Now:T}\n{symbol}\nОжидание продажи..\nИзменение цены:{priceChange}%\nЦена покупки:{lastBuyPrice}\nЦена:{lastPrice:F2}\nema{emaFastLength}:{emaFast:F2}\nema{emaSlowLength}:{emaSlow:F2}";
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
