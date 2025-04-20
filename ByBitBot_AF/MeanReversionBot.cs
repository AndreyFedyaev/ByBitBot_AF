using Bybit.Net.Clients;
using CryptoExchange.Net.Authentication;
using Bybit.Net.Enums;
using CryptoExchange.Net.CommonObjects;

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
        public decimal downLevel_6 { get; set; }
        public decimal downLevel_7 { get; set; }
        public decimal downLevel_8 { get; set; }
        public decimal downLevel_9 { get; set; }
        public decimal downLevel_10 { get; set; }

    }

    public class Orders
    {
        public decimal buyPrice { get; set; }               //цена ордера на покупку для текущего уровня
        public decimal sellPrice { get; set; }              //цена ордера на продажу для текущего уровня
        public string buyOrderID { get; set; }              //ID ордера на покупку, если ордера нет = 0
        public string sellOrderID { get; set; }             //ID ордера на продажу, если ордера нет = 0
    }

    public class MeanReversionBot
    {

        //параметры из строки запуска контейнера Docker:
        private string docker_apiKey = Environment.GetEnvironmentVariable("apikey") ?? "";
        private string docker_apiSecret = Environment.GetEnvironmentVariable("apisecret") ?? "";
        private string docker_coin = Environment.GetEnvironmentVariable("coin") ?? "";
        private string docker_currency = Environment.GetEnvironmentVariable("currency") ?? "";
        //private string docker_emaFastLength = Environment.GetEnvironmentVariable("emafastlength") ?? "";
        //private string docker_emaSlowLength = Environment.GetEnvironmentVariable("emaslowlength") ?? "";
        //private string docker_candleInterval = Environment.GetEnvironmentVariable("candleInterval") ?? "";
        private string docker_cycle = Environment.GetEnvironmentVariable("cycle") ?? "";

        //параметры из конфигурации:     
        private string apiKey;                                  //ключ API KEY из ByBIT
        private string apiSecret;                               //ключ API SECRET из ByBIT
        private string coin;                                    //монета
        private string currency;                                //валюта
        private string symbol;                                  //торговая пара
        //private int emaFastLength;                              //длина экспоненциальной скользящей средней на короткий период
        //private int emaSlowLength;                              //длина экспоненциальной скользящей средней на длинный период
        //private KlineInterval candleInterval;                   //интервал свечей
        private TimeSpan cycle;                                 //таймер работы (цикл работы)

        //свойства
        private readonly GetCoinData _getCoinData;
        private readonly GetWalletData _getWalletData;
        private readonly BybitRestClient _client;
        private readonly TelegrammBot _telegrammBot;
        private decimal? lastBuyPrice { get; set; } = null;           //цена последней покупки

        //свойства монеты
        private decimal lastPrice { get; set; }             //последняя цена
        //private decimal emaFast { get; set; }               //величина экспоненциальной скользящей средней на короткий период    
        //private decimal emaSlow { get; set; }               //величина экспоненциальной скользящей средней на длинный период

        //grid стратегия
        private List<Orders> ordersList = new List<Orders>();
        Grid gridInfo = new Grid();
        private decimal gridLevel_newHorizont = 0.005m;     // +0.5%
        private decimal gridLevel_1 = 0.005m;       // -0.5%
        private decimal gridLevel_2 = 0.01m;        // -1%
        private decimal gridLevel_3 = 0.015m;       // -1.5%
        private decimal gridLevel_4 = 0.02m;        // -2%
        private decimal gridLevel_5 = 0.025m;       // -2.5%
        private decimal gridLevel_6 = 0.03m;        // -3%
        private decimal gridLevel_7 = 0.035m;       // -3.5%
        private decimal gridLevel_8 = 0.04m;        // -4%
        private decimal gridLevel_9 = 0.045m;       // -4.5%
        private decimal gridLevel_10 = 0.05m;       // -5%

        private decimal buyCount = 2;

        public MeanReversionBot()
        {
            ReadConfig();

            _client = new BybitRestClient(options =>
            {
                options.ApiCredentials = new ApiCredentials(apiKey, apiSecret);
            });

            _getCoinData = new GetCoinData(_client);
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

            //emaFastLength = Convert.ToInt32(docker_emaFastLength);
            //emaSlowLength = Convert.ToInt32(docker_emaSlowLength);

            //string _candleInterval = docker_candleInterval;
            //switch (_candleInterval)
            //{
            //    case "OneMinute":
            //        candleInterval = KlineInterval.OneMinute;
            //        break;
            //    case "ThreeMinutes":
            //        candleInterval = KlineInterval.ThreeMinutes;
            //        break;
            //    case "FiveMinutes":
            //        candleInterval = KlineInterval.FiveMinutes;
            //        break;
            //    case "FifteenMinutes":
            //        candleInterval = KlineInterval.FifteenMinutes;
            //        break;
            //    case "ThirtyMinutes":
            //        candleInterval = KlineInterval.ThirtyMinutes;
            //        break;
            //    case "OneHour":
            //        candleInterval = KlineInterval.OneHour;
            //        break;
            //    case "TwoHours":
            //        candleInterval = KlineInterval.TwoHours;
            //        break;
            //    case "FourHours":
            //        candleInterval = KlineInterval.FourHours;
            //        break;
            //    case "SixHours":
            //        candleInterval = KlineInterval.SixHours;
            //        break;
            //    case "TwelveHours":
            //        candleInterval = KlineInterval.TwelveHours;
            //        break;
            //    case "OneDay":
            //        candleInterval = KlineInterval.OneDay;
            //        break;
            //    case "OneWeek":
            //        candleInterval = KlineInterval.OneWeek;
            //        break;
            //    case "OneMonth":
            //        candleInterval = KlineInterval.OneMonth;
            //        break;
            //    default:
            //        candleInterval = KlineInterval.OneMinute;
            //        break;
            //}

            cycle = TimeSpan.FromSeconds(Convert.ToInt32(docker_cycle));
        }

        private void OrdersLog()
        {
            string tbMessage = $"Список ордеров:" + "\n";

            for (int i = 0; i < ordersList.Count; i++)
            {
                tbMessage = tbMessage + $"Level {i + 1}:" + "\n";
                tbMessage = tbMessage + $"buyPrice = {ordersList[i].buyPrice}" + "\n";
                tbMessage = tbMessage + $"sellPrice = {ordersList[i].sellPrice}" + "\n";
                tbMessage = tbMessage + $"buyOrderID = {ordersList[i].buyOrderID}" + "\n";
                tbMessage = tbMessage + $"sellOrderID = {ordersList[i].sellOrderID}" + "\n";
            }

            Console.WriteLine(tbMessage);
            _telegrammBot.TgBotSendMessage(tbMessage);
            _telegrammBot.ordersInfo = tbMessage;
        }
        private async Task SetBuyOrder(decimal orderPrice)
        {
            var count = Math.Floor((buyCount / orderPrice) * 1000) / 1000;
            var result = await _client.V5Api.Trading.PlaceOrderAsync(
                Bybit.Net.Enums.Category.Spot,
                symbol,
                Bybit.Net.Enums.OrderSide.Buy,
                Bybit.Net.Enums.NewOrderType.Limit,
                count,
                orderPrice
                );

            if (result.Success)
            {
                var order = ordersList.FirstOrDefault(p => p.buyPrice == orderPrice);
                if (order != null)
                {
                    order.buyOrderID = result.Data.OrderId;
                    OrdersLog();
                }
            }
        }

        private async Task<string> SetSellOrder(decimal count, decimal orderPrice)
        {
            var result = await _client.V5Api.Trading.PlaceOrderAsync(
                Bybit.Net.Enums.Category.Spot,
                symbol,
                Bybit.Net.Enums.OrderSide.Sell,
                Bybit.Net.Enums.NewOrderType.Limit,
                count,
                orderPrice
                );

            if (result.Success)
            {
                return result.Data.OrderId;
            }
            else { return string.Empty; }
        }

        private async void GridUpdate(decimal _lastPrice)
        {
            //получаем список всех активных лимитных ордеров для указанной торговой пары
            var result = await _client.V5Api.Trading.GetOrdersAsync(
                Bybit.Net.Enums.Category.Spot,
                symbol
            );

            //если ордера есть - удаляем
            if (result != null && result.Success && result.Data.List.Count() > 0)
            {
                foreach (var item in result.Data.List)
                {
                    var id = item.OrderId;

                    var result2 = await _client.V5Api.Trading.CancelOrderAsync(
                        Bybit.Net.Enums.Category.Spot, 
                        symbol,
                        id);
                }
            }

            //если ордеров нет - строим новую сетку
            if (result != null && result.Success && result.Data.List.Count() == 0)
            {
                //формируем новую сетку
                gridInfo.newHorizont = Math.Round(_lastPrice + (_lastPrice * gridLevel_newHorizont), 2);
                gridInfo.horizont = Math.Round(_lastPrice, 2);
                gridInfo.downLevel_1 = Math.Round(_lastPrice - (_lastPrice * gridLevel_1), 2);
                gridInfo.downLevel_2 = Math.Round(_lastPrice - (_lastPrice * gridLevel_2), 2);
                gridInfo.downLevel_3 = Math.Round(_lastPrice - (_lastPrice * gridLevel_3), 2);
                gridInfo.downLevel_4 = Math.Round(_lastPrice - (_lastPrice * gridLevel_4), 2);
                gridInfo.downLevel_5 = Math.Round(_lastPrice - (_lastPrice * gridLevel_5), 2);
                gridInfo.downLevel_6 = Math.Round(_lastPrice - (_lastPrice * gridLevel_6), 2);
                gridInfo.downLevel_7 = Math.Round(_lastPrice - (_lastPrice * gridLevel_7), 2);
                gridInfo.downLevel_8 = Math.Round(_lastPrice - (_lastPrice * gridLevel_8), 2);
                gridInfo.downLevel_9 = Math.Round(_lastPrice - (_lastPrice * gridLevel_9), 2);
                gridInfo.downLevel_10 = Math.Round(_lastPrice - (_lastPrice * gridLevel_10), 2);


                string gridMessage = $"Обновление сетки:" + "\n";
                gridMessage = gridMessage + $"Уровень обновления UP: {gridInfo.newHorizont}" + "\n";
                gridMessage = gridMessage + $"Горизонт сетки: {gridInfo.horizont}" + "\n";
                gridMessage = gridMessage + $"-{gridLevel_1 * 100:F1}%: {gridInfo.downLevel_1}" + "\n";
                gridMessage = gridMessage + $"-{gridLevel_2 * 100:F1}%: {gridInfo.downLevel_2}" + "\n";
                gridMessage = gridMessage + $"-{gridLevel_3 * 100:F1}%: {gridInfo.downLevel_3}" + "\n";
                gridMessage = gridMessage + $"-{gridLevel_4 * 100:F1}%: {gridInfo.downLevel_4}" + "\n";
                gridMessage = gridMessage + $"-{gridLevel_5 * 100:F1}%: {gridInfo.downLevel_5}" + "\n";
                gridMessage = gridMessage + $"-{gridLevel_6 * 100:F1}%: {gridInfo.downLevel_6}" + "\n";
                gridMessage = gridMessage + $"-{gridLevel_7 * 100:F1}%: {gridInfo.downLevel_7}" + "\n";
                gridMessage = gridMessage + $"-{gridLevel_8 * 100:F1}%: {gridInfo.downLevel_8}" + "\n";
                gridMessage = gridMessage + $"-{gridLevel_9 * 100:F1}%: {gridInfo.downLevel_9}" + "\n";
                gridMessage = gridMessage + $"-{gridLevel_10 * 100:F1}%: {gridInfo.downLevel_10}" + "\n";


                Console.WriteLine(gridMessage);
                _telegrammBot.TgBotSendMessage(gridMessage);
                _telegrammBot.gridInfo = gridMessage;

                ordersList = new List<Orders>();

                ordersList.Add(new Orders
                {
                    buyPrice = gridInfo.downLevel_1,
                    sellPrice = gridInfo.horizont,
                    buyOrderID = "",
                    sellOrderID = ""
                });
                ordersList.Add(new Orders
                {
                    buyPrice = gridInfo.downLevel_2,
                    sellPrice = gridInfo.downLevel_1,
                    buyOrderID = "",
                    sellOrderID = ""
                });
                ordersList.Add(new Orders
                {
                    buyPrice = gridInfo.downLevel_3,
                    sellPrice = gridInfo.downLevel_2,
                    buyOrderID = "",
                    sellOrderID = ""
                });
                ordersList.Add(new Orders
                {
                    buyPrice = gridInfo.downLevel_4,
                    sellPrice = gridInfo.downLevel_3,
                    buyOrderID = "",
                    sellOrderID = ""
                });
                ordersList.Add(new Orders
                {
                    buyPrice = gridInfo.downLevel_5,
                    sellPrice = gridInfo.downLevel_4,
                    buyOrderID = "",
                    sellOrderID = ""
                });
                ordersList.Add(new Orders
                {
                    buyPrice = gridInfo.downLevel_6,
                    sellPrice = gridInfo.downLevel_5,
                    buyOrderID = "",
                    sellOrderID = ""
                });
                ordersList.Add(new Orders
                {
                    buyPrice = gridInfo.downLevel_7,
                    sellPrice = gridInfo.downLevel_6,
                    buyOrderID = "",
                    sellOrderID = ""
                });
                ordersList.Add(new Orders
                {
                    buyPrice = gridInfo.downLevel_8,
                    sellPrice = gridInfo.downLevel_7,
                    buyOrderID = "",
                    sellOrderID = ""
                });
                ordersList.Add(new Orders
                {
                    buyPrice = gridInfo.downLevel_9,
                    sellPrice = gridInfo.downLevel_8,
                    buyOrderID = "",
                    sellOrderID = ""
                });
                ordersList.Add(new Orders
                {
                    buyPrice = gridInfo.downLevel_10,
                    sellPrice = gridInfo.downLevel_9,
                    buyOrderID = "",
                    sellOrderID = ""
                });
            }
        }

        public async Task StartLoopAsync()
        {
            Console.WriteLine("Бот запущен.....\n");

            while (true)
            {
                try
                {
                    //получаем баланс портфеля
                    await _getWalletData.UpdateWalletData();

                    string walletInfo = $"{coin}: {_getWalletData.AssetBalance:F5}" + "\n";
                    walletInfo = walletInfo + $"{currency}: {_getWalletData.TotalAvailableBalance:F3}" + "\n";
                    walletInfo = walletInfo + $"Wallet: {_getWalletData.TotalEquity:F3}" + "\n";
                    _telegrammBot.lastBalanceInfo = walletInfo;

                    //получение текущей цены торговой пары
                    lastPrice = await _getCoinData.GetCurrentPriceAsync(symbol);
                    _telegrammBot.info = $"Текущая цена {symbol}: {lastPrice:F3}";

                    //формируем сетку при запуске бота или обновляем сетку при увеличении текуще цены
                    if (gridInfo.horizont == 0 && lastPrice >= gridInfo.newHorizont)
                    {
                        GridUpdate(lastPrice);
                    }

                    //ищем buyOrderID в списке исполненных (архивных)
                    foreach (var order in ordersList)
                    {
                        if (order.buyOrderID.Trim() == "") continue;

                        var ordersHistory = await _client.V5Api.Trading.GetOrderHistoryAsync(
                            category: Category.Spot,
                            orderId: order.buyOrderID
                        );

                        if (ordersHistory != null && ordersHistory.Success && ordersHistory.Data.List.Count() > 0)
                        {
                            string id = ordersHistory.Data.List.First().OrderId;
                            decimal quantity = ordersHistory.Data.List.First().Quantity;
                            decimal commission = ordersHistory.Data.List.First().ExecutedFee ?? 0;

                            decimal takeProfitCount = Math.Floor((quantity - commission) * 1000) / 1000;

                            if (id == order.buyOrderID)
                            {
                                string sellOrderID = await SetSellOrder(takeProfitCount, order.sellPrice);
                                if (sellOrderID.Trim() != "")
                                {
                                    order.sellOrderID = sellOrderID;
                                    order.buyOrderID = "";

                                    OrdersLog();
                                }

                            }
                        }
                    }

                    //ищем sellOrderID в списке исполненных (архивных)
                    foreach (var order in ordersList)
                    {
                        if (order.sellOrderID.Trim() == "") continue;

                        var ordersHistory = await _client.V5Api.Trading.GetOrderHistoryAsync(
                            category: Category.Spot,
                            orderId: order.sellOrderID
                        );

                        if (ordersHistory != null && ordersHistory.Success && ordersHistory.Data.List.Count() > 0)
                        {
                            string id = ordersHistory.Data.List.First().OrderId;
                            decimal quantity = ordersHistory.Data.List.First().Quantity;
                            decimal commission = ordersHistory.Data.List.First().ExecutedFee ?? 0;

                            if (id == order.sellOrderID)
                            {
                                order.sellOrderID = "";

                                OrdersLog();
                            }
                        }
                    }

                    foreach(var order in ordersList)
                    {
                        if (order.buyOrderID.Trim() == "" && order.sellOrderID.Trim() == "")
                        {
                            await SetBuyOrder(order.buyPrice);
                        }
                    }




                    //получение текущих значений EMA
                    //var (emaFastResult, emaSlowResult) = await _getCoinData.GetEmaValuesAsync(emaFastLength, emaSlowLength);
                    //emaFast = emaFastResult;
                    //emaSlow = emaSlowResult;

                    //string w1 = "";
                    //if (lastPrice > emaFast) w1 = ">"; else w1 = "<";
                    //string w2 = "";
                    //if (emaFast > emaSlow) w2 = ">"; else w2 = "<";

                    //if (lastBuyPrice == null)
                    //{
                    //    Console.WriteLine($"[{DateTime.Now:T}] | {symbol} | Ожидание покупки.. | Цена:{lastPrice:F2} {w1} ema{emaFastLength}:{emaFast:F2} {w2} ema{emaSlowLength}:{emaSlow:F2} | {currency}:{_getWalletData.TotalAvailableBalance:F3}, {coin}:{_getWalletData.AssetBalance:F5}, Wallet:{_getWalletData.TotalEquity:F3}");

                    //    _telegrammBot.lastLoggingMessage = $"[{DateTime.Now:T}]\n{coin}/{currency}: ожидание покупки..\nЦена: {lastPrice:F3}\nEma{emaFastLength}: {emaFast:F3}\nEma{emaSlowLength}: {emaSlow:F3}";
                    //}
                    //else
                    //{
                    //    decimal priceChange = ((decimal)lastPrice * 100 / (decimal)lastBuyPrice) - 100;
                    //    string w3 = "";
                    //    if (lastBuyPrice > lastPrice) w3 = ">"; else w3 = "<";

                    //    Console.WriteLine($"[{DateTime.Now:T}] | {symbol} | Ожидание продажи.. | {priceChange:F2}% | Цена покупки:{lastBuyPrice} {w3} Цена:{lastPrice:F2} {w1} ema{emaFastLength}:{emaFast:F2} {w2} ema{emaSlowLength}:{emaSlow:F2} | USDT:{_getWalletData.TotalAvailableBalance:F3}, SOL:{_getWalletData.AssetBalance:F5}, Wallet:{_getWalletData.TotalEquity:F3}");

                    //    _telegrammBot.lastLoggingMessage = $"[{DateTime.Now:T}]\n{coin}/{currency}: ожидание продажи..\nИзменение цены: {priceChange:F2}%\nЦена покупки: {lastBuyPrice:F3}\nЦена: {lastPrice:F3}\nEma{emaFastLength}: {emaFast:F3}\nEma{emaSlowLength}: {emaSlow:F3}";
                    //}


                    ////проверка условий для покупки
                    //var buy = AnalyzeBuy();
                    //if (buy)
                    //{
                    //    if (lastBuyPrice == null) //если покупок еще небыло
                    //    {
                    //        var buyAmount = CalculateBuyAmountUSDT();

                    //        //await Buy(symbol, buyAmount, MarketUnit.QuoteAsset);
                    //    }
                    //}

                    ////проверка условий для продажи
                    //var sell = AnalyzeSell();
                    //if (sell)
                    //{
                    //    //можно продавать
                    //    if (lastBuyPrice != null)
                    //    {
                    //        //если есть покупки для продажи
                    //        //await Sell();
                    //    }
                    //}


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
        //private bool AnalyzeBuy()
        //{
        //    bool result = false;

        //    //if (lastPrice > emaFast && emaFast > emaSlow) result = true;
        //    if (emaFast > emaSlow) result = true;

        //    return result;
        //}

        /// <summary>
        /// Анализ условий для продажи
        /// </summary>
        /// <returns>true - можно продавать!</returns>
        //private bool AnalyzeSell()
        //{
        //    bool result = false;

        //    //if (lastBuyPrice < lastPrice && lastPrice < emaFast && emaFast < emaSlow) result = true;
        //    if (emaFast < emaSlow) result = true;

        //    return result;
        //}

        /// <summary>
        /// Определение суммы покупки в USDT
        /// </summary>
        /// <returns>величина покупки (decimal)</returns>
        //private decimal CalculateBuyAmountUSDT()
        //{
        //    decimal result = 0;

        //    if (_getWalletData != null && _getWalletData.TotalAvailableBalance != null)
        //    {
        //        var amount = Math.Round((decimal)_getWalletData.TotalAvailableBalance * 0.98m, 2);     // 98% от всех доступных средств
        //        var minOrderValue = _getWalletData.MinOrderBuyValue;    //минимальная сумма покупки для текущей валюты

        //        if (amount < minOrderValue)
        //        {
        //            Console.WriteLine($"[{DateTime.Now:T}] | Недостаточно USDT: {amount:F3} (нужно хотя бы {minOrderValue} USDT)");
        //            result = 0;
        //        }

        //        result = amount;
        //    }

        //    return result;
        //}

        /// <summary>
        /// Определение суммы продажи
        /// </summary>
        /// <returns>величина продажи (decimal)</returns>
        //private decimal CalculateSellAmount()
        //{
        //    decimal result = 0;



        //    return result;
        //}

        /// <summary>
        /// Покупка
        /// </summary>
        /// <param name="bBymbol">торговая пара</param>
        /// <param name="bAmount">величина покупки</param>
        /// <param name="bMarketUnit">единица измерения для рыночного ордера (например для BTCUSDT: "MarketUnit.BaseAsset" - в BTC, "MarketUnit.QuoteAsset" - в USDT)</param>
        //private async Task Buy(string bBymbol, decimal bAmount, MarketUnit bMarketUnit)
        //{
        //    try
        //    {
        //        var result = await _client.V5Api.Trading.PlaceOrderAsync(
        //                Bybit.Net.Enums.Category.Spot,                          // Спотовая торговля
        //                bBymbol,                                                // Пара
        //                Bybit.Net.Enums.OrderSide.Buy,                          // Сторона сделки
        //                Bybit.Net.Enums.NewOrderType.Market,                    // Тип ордера: рыночный
        //                bAmount,                                                // Кол-во
        //                marketUnit: bMarketUnit
        //                );

        //        if (result.Success)
        //        {
        //            string message = $"[{DateTime.Now:T}] | Покупка {coin} на сумму: {bAmount} USDT | Цена: {lastPrice}";

        //            Console.WriteLine(message);
        //            _telegrammBot.TgBotSendMessage(message);
        //            _telegrammBot.BuySellArchive.Add(message);

        //            lastBuyPrice = lastPrice;
        //        }
        //        else
        //        {
        //            Console.WriteLine($"Ошибка ордера: {result.Error}");
        //            _telegrammBot.TgBotSendMessage($"Ошибка ордера: {result.Error}");
        //        }
        //    }
        //    catch(Exception ex)
        //    {
        //        Console.WriteLine($"[{DateTime.Now:T}] | Exception: {ex.Message}");
        //    }
        //}

        /// <summary>
        /// Продажа 
        /// </summary>
        //private async Task Sell()
        //{
        //    try
        //    {
        //        // Достаём баланс актива (например, SOL)
        //        var assetBalance = _getWalletData.AssetBalance;

        //        // Достаём минимально допустимое количество для продажи
        //        var minOrderQuantity = _getWalletData.MinOrderSellValue;

        //        // Оставляем небольшой запас и округляем
        //        decimal quantityToSell = Math.Round((decimal)assetBalance * 0.98m, 3); // 98%, 0.001 precision

        //        if (quantityToSell < minOrderQuantity)
        //        {
        //            Console.WriteLine($"[{DateTime.Now:T}] | Недостаточно {symbol} для продажи: {quantityToSell:F3} (минимум {minOrderQuantity})");
        //            return;
        //        }

        //        // Отправляем маркет-ордер на продажу
        //        var result = await _client.V5Api.Trading.PlaceOrderAsync(
        //            Bybit.Net.Enums.Category.Spot,
        //            symbol,
        //            Bybit.Net.Enums.OrderSide.Sell,
        //            Bybit.Net.Enums.NewOrderType.Market,
        //            quantityToSell
        //        );

        //        if (result.Success)
        //        {
        //            string message = $"[{DateTime.Now:T}] | Продажа {coin}: {quantityToSell} | Цена: {lastPrice}";

        //            Console.WriteLine(message);
        //            _telegrammBot.TgBotSendMessage(message);
        //            _telegrammBot.BuySellArchive.Add(message);

        //            lastBuyPrice = null;
        //        }
        //        else
        //        {
        //            Console.WriteLine($"Ошибка ордера: {result.Error}");
        //            _telegrammBot.TgBotSendMessage($"Ошибка ордера: {result.Error}");
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine($"[{DateTime.Now:T}] | Exception: {ex.Message}");
        //    }
        //}




    }
}
