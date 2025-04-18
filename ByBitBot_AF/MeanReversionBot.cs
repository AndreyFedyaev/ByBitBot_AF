using Bybit.Net.Clients;
using CryptoExchange.Net.Authentication;
using Bybit.Net.Enums;

namespace ByBitBot_AF
{
    public class Grid
    {
        public decimal newHorizont { get; set; }
        public decimal horizont { get; set; }
        public decimal downLevel_1 { get; set; }
        public decimal downLevel_2 { get; set; }
        public decimal downLevel_3 { get; set; }
        public decimal downLevel_4 { get; set; }
        public decimal downLevel_5 { get; set; }

    }

    public class Orders
    {
        public decimal orderLevel { get; set; }                 //цена текущего уровня
        public string orderID { get; set; }                        //если ордер есть, то будет ID, если нет - 0
        
    }

    public class MeanReversionBot
    {

        //параметры из строки запуска контейнера Docker:
        private string docker_apiKey = Environment.GetEnvironmentVariable("apikey") ?? "IIC9lG1jXO4FrbMgrW";
        private string docker_apiSecret = Environment.GetEnvironmentVariable("apisecret") ?? "AhebRU0bZee2eoRrJGbCgQdtL6GBzl245bgL";
        private string docker_coin = Environment.GetEnvironmentVariable("coin") ?? "SOL";
        private string docker_currency = Environment.GetEnvironmentVariable("currency") ?? "USDT";
        private string docker_emaFastLength = Environment.GetEnvironmentVariable("emafastlength") ?? "20";
        private string docker_emaSlowLength = Environment.GetEnvironmentVariable("emaslowlength") ?? "50";
        private string docker_candleInterval = Environment.GetEnvironmentVariable("candleInterval") ?? "FiveMinutes";
        private string docker_cycle = Environment.GetEnvironmentVariable("cycle") ?? "10";

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

        //grid стратегия
        private List<Orders> ordersList = new List<Orders>();
        Grid gridInfo = new Grid();
        private decimal gridLevel_newHorizont = 0.01m;     // +1%
        private decimal gridLevel_1 = 0.01m;    // -1%
        private decimal gridLevel_2 = 0.02m;    // -2%
        private decimal gridLevel_3 = 0.03m;    // -3%

        private decimal buyCount = 2;

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

        private void GridBuild(decimal _lastPrice)
        {
            if(gridInfo.horizont == 0)
            {
                GridUpdate(_lastPrice);
            }

            if (_lastPrice >= gridInfo.newHorizont)
            {
                GridUpdate(_lastPrice);
            }
        }

        private async void GridUpdate(decimal _lastPrice)
        {
            gridInfo.newHorizont = Math.Round(_lastPrice + (_lastPrice * gridLevel_newHorizont),2);
            gridInfo.horizont = Math.Round(_lastPrice,2);
            gridInfo.downLevel_1 = Math.Round(_lastPrice - (_lastPrice * gridLevel_1), 2);
            gridInfo.downLevel_2 = Math.Round(_lastPrice - (_lastPrice * gridLevel_2), 2);
            gridInfo.downLevel_3 = Math.Round(_lastPrice - (_lastPrice * gridLevel_3), 2);


            Console.WriteLine($"Обновление сетки:");
            Console.WriteLine($"Следующий уровень обновления UP: {gridInfo.newHorizont}");
            Console.WriteLine($"Текущая цена: {_lastPrice:F3}");
            Console.WriteLine($"-{gridLevel_1 * 100:F1}%: {gridInfo.downLevel_1}");
            Console.WriteLine($"-{gridLevel_2 * 100:F1}%: {gridInfo.downLevel_2}");
            Console.WriteLine($"-{gridLevel_3 * 100:F1}%: {gridInfo.downLevel_3}");


            var countLevel1 = Math.Floor((buyCount / gridInfo.downLevel_1) * 1000) / 1000;             //настроить округление в меньшую сторону
            var resultLevel1 = await _client.V5Api.Trading.PlaceOrderAsync(
                Bybit.Net.Enums.Category.Spot,
                symbol,
                Bybit.Net.Enums.OrderSide.Buy,
                Bybit.Net.Enums.NewOrderType.Limit,
                countLevel1,
                gridInfo.downLevel_1
                );
            if (resultLevel1.Success)
            {
                ordersList.Add(new Orders
                {
                    orderID = resultLevel1.Data.OrderId,
                    orderLevel = gridInfo.downLevel_1
                });
            }

            var countLevel2 = Math.Floor((buyCount / gridInfo.downLevel_2) * 1000) / 1000;             //настроить округление в меньшую сторону
            var resultLevel2 = await _client.V5Api.Trading.PlaceOrderAsync(
                Bybit.Net.Enums.Category.Spot,
                symbol,
                Bybit.Net.Enums.OrderSide.Buy,
                Bybit.Net.Enums.NewOrderType.Limit,
                countLevel2,
                gridInfo.downLevel_2
                );
            if (resultLevel2.Success)
            {
                ordersList.Add(new Orders
                {
                    orderID = resultLevel2.Data.OrderId,
                    orderLevel = gridInfo.downLevel_2
                });
            }

            var countLevel3 = Math.Floor((buyCount / gridInfo.downLevel_3) * 1000) / 1000;             //настроить округление в меньшую сторону
            var resultLevel3 = await _client.V5Api.Trading.PlaceOrderAsync(
                Bybit.Net.Enums.Category.Spot,
                symbol,
                Bybit.Net.Enums.OrderSide.Buy,
                Bybit.Net.Enums.NewOrderType.Limit,
                countLevel3,
                gridInfo.downLevel_3
                );
            if (resultLevel3.Success)
            {
                ordersList.Add(new Orders
                {
                    orderID = resultLevel3.Data.OrderId,
                    orderLevel = gridInfo.downLevel_3
                });
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

                    //_telegrammBot.lastBalanceInfo = $"{coin}:  {_getWalletData.AssetBalance:F5}\n{currency}:  {_getWalletData.TotalAvailableBalance:F3}\nWallet:  {_getWalletData.TotalEquity:F3}";

                    ////определяем есть ли активные покупки
                    //var searchBuy = await _client.V5Api.Trading.GetUserTradesAsync(
                    //    category: Category.Spot,
                    //    symbol: symbol,
                    //    limit: 1);
                    //if (searchBuy != null && searchBuy.Success)
                    //{
                    //    var trade = searchBuy.Data.List.FirstOrDefault();
                    //    if (trade != null && trade.FeeAsset == coin && trade.Side == OrderSide.Buy)
                    //    {
                    //        lastBuyPrice = trade.Price;
                    //    }
                    //}

                    var result = await _client.V5Api.Trading.GetOrdersAsync(
                        Bybit.Net.Enums.Category.Spot
                        );
                    var history = await _client.V5Api.Trading.GetOrderHistoryAsync(
    category: Category.Spot
);

                    //получение текущей цены SOL/USDT
                    lastPrice = await _getCoinData.GetCurrentPriceAsync(symbol);

                    //расчет сетки
                    GridBuild(lastPrice);

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
                        Console.WriteLine($"[{DateTime.Now:T}] | {symbol} | Ожидание покупки.. | Цена:{lastPrice:F2} {w1} ema{emaFastLength}:{emaFast:F2} {w2} ema{emaSlowLength}:{emaSlow:F2} | {currency}:{_getWalletData.TotalAvailableBalance:F3}, {coin}:{_getWalletData.AssetBalance:F5}, Wallet:{_getWalletData.TotalEquity:F3}");

                        _telegrammBot.lastLoggingMessage = $"[{DateTime.Now:T}]\n{coin}/{currency}: ожидание покупки..\nЦена: {lastPrice:F3}\nEma{emaFastLength}: {emaFast:F3}\nEma{emaSlowLength}: {emaSlow:F3}";
                    }
                    else
                    {
                        decimal priceChange = ((decimal)lastPrice * 100 / (decimal)lastBuyPrice) - 100;
                        string w3 = "";
                        if (lastBuyPrice > lastPrice) w3 = ">"; else w3 = "<";

                        Console.WriteLine($"[{DateTime.Now:T}] | {symbol} | Ожидание продажи.. | {priceChange:F2}% | Цена покупки:{lastBuyPrice} {w3} Цена:{lastPrice:F2} {w1} ema{emaFastLength}:{emaFast:F2} {w2} ema{emaSlowLength}:{emaSlow:F2} | USDT:{_getWalletData.TotalAvailableBalance:F3}, SOL:{_getWalletData.AssetBalance:F5}, Wallet:{_getWalletData.TotalEquity:F3}");

                        _telegrammBot.lastLoggingMessage = $"[{DateTime.Now:T}]\n{coin}/{currency}: ожидание продажи..\nИзменение цены: {priceChange:F2}%\nЦена покупки: {lastBuyPrice:F3}\nЦена: {lastPrice:F3}\nEma{emaFastLength}: {emaFast:F3}\nEma{emaSlowLength}: {emaSlow:F3}";
                    }


                    //проверка условий для покупки
                    var buy = AnalyzeBuy();
                    if (buy)
                    {
                        if (lastBuyPrice == null) //если покупок еще небыло
                        {
                            var buyAmount = CalculateBuyAmountUSDT();

                            //await Buy(symbol, buyAmount, MarketUnit.QuoteAsset);
                        }
                    }

                    //проверка условий для продажи
                    var sell = AnalyzeSell();
                    if (sell)
                    {
                        //можно продавать
                        if (lastBuyPrice != null)
                        {
                            //если есть покупки для продажи
                            //await Sell();
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
        /// Анализ условий для покупки
        /// </summary>
        /// <returns>true - можно покупать!</returns>
        private bool AnalyzeBuy()
        {
            bool result = false;

            //if (lastPrice > emaFast && emaFast > emaSlow) result = true;
            if (emaFast > emaSlow) result = true;

            return result;
        }

        /// <summary>
        /// Анализ условий для продажи
        /// </summary>
        /// <returns>true - можно продавать!</returns>
        private bool AnalyzeSell()
        {
            bool result = false;

            //if (lastBuyPrice < lastPrice && lastPrice < emaFast && emaFast < emaSlow) result = true;
            if (emaFast < emaSlow) result = true;

            return result;
        }

        /// <summary>
        /// Определение суммы покупки в USDT
        /// </summary>
        /// <returns>величина покупки (decimal)</returns>
        private decimal CalculateBuyAmountUSDT()
        {
            decimal result = 0;

            if (_getWalletData != null && _getWalletData.TotalAvailableBalance != null)
            {
                var amount = Math.Round((decimal)_getWalletData.TotalAvailableBalance * 0.98m, 2);     // 98% от всех доступных средств
                var minOrderValue = _getWalletData.MinOrderBuyValue;    //минимальная сумма покупки для текущей валюты

                if (amount < minOrderValue)
                {
                    Console.WriteLine($"[{DateTime.Now:T}] | Недостаточно USDT: {amount:F3} (нужно хотя бы {minOrderValue} USDT)");
                    result = 0;
                }

                result = amount;
            }

            return result;
        }

        /// <summary>
        /// Определение суммы продажи
        /// </summary>
        /// <returns>величина продажи (decimal)</returns>
        private decimal CalculateSellAmount()
        {
            decimal result = 0;



            return result;
        }

        /// <summary>
        /// Покупка
        /// </summary>
        /// <param name="bBymbol">торговая пара</param>
        /// <param name="bAmount">величина покупки</param>
        /// <param name="bMarketUnit">единица измерения для рыночного ордера (например для BTCUSDT: "MarketUnit.BaseAsset" - в BTC, "MarketUnit.QuoteAsset" - в USDT)</param>
        private async Task Buy(string bBymbol, decimal bAmount, MarketUnit bMarketUnit)
        {
            try
            {
                var result = await _client.V5Api.Trading.PlaceOrderAsync(
                        Bybit.Net.Enums.Category.Spot,                          // Спотовая торговля
                        bBymbol,                                                // Пара
                        Bybit.Net.Enums.OrderSide.Buy,                          // Сторона сделки
                        Bybit.Net.Enums.NewOrderType.Market,                    // Тип ордера: рыночный
                        bAmount,                                                // Кол-во
                        marketUnit: bMarketUnit
                        );

                if (result.Success)
                {
                    string message = $"[{DateTime.Now:T}] | Покупка {coin} на сумму: {bAmount} USDT | Цена: {lastPrice}";

                    Console.WriteLine(message);
                    _telegrammBot.TgBotSendMessage(message);
                    _telegrammBot.BuySellArchive.Add(message);

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
                    string message = $"[{DateTime.Now:T}] | Продажа {coin}: {quantityToSell} | Цена: {lastPrice}";

                    Console.WriteLine(message);
                    _telegrammBot.TgBotSendMessage(message);
                    _telegrammBot.BuySellArchive.Add(message);

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
