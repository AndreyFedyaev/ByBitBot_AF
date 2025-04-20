using Bybit.Net.Clients;
using Bybit.Net.Enums;
using CryptoExchange.Net.CommonObjects;
using Skender.Stock.Indicators;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ByBit_TestBot.Program;

namespace ByBitBot_AF
{
    class GetCoinData
    {
        private readonly BybitRestClient _client;
        //private readonly string _symbol;
        //private readonly KlineInterval _candleInterval;
        public GetCoinData(BybitRestClient client)
        {
            _client = client;
            //_symbol = symbol;
            //_candleInterval = candleInterval;
        }

        /// <summary>
        /// Получает текущую цену указанной торговой пары
        /// </summary>
        /// <param name="readSymbol">торговая пара для получения текущей цены</param>
        public async Task<decimal> GetCurrentPriceAsync(string readSymbol)
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
        /// Рассчитывает значения короткой и длинной экспоненциальной скользящей средней
        /// </summary>
        //public async Task<(decimal resultEmaFast, decimal resultEmaSlow)> GetEmaValuesAsync(int emaFastLength, int emaSlowLength)
        //{
        //    var candles = await GetLastCandlesAsync(200);      //получаем информацию о 200 последних свечах
        //    var quotes = candles.Select(c => new Quote
        //    {
        //        Date = c.DT,
        //        Open = c.Open,
        //        High = c.High,
        //        Low = c.Low,
        //        Close = c.Close,
        //        Volume = 0
        //    }).ToList();

        //    var emaFastData = quotes.GetEma(emaFastLength).LastOrDefault()?.Ema;
        //    var emaSlowData = quotes.GetEma(emaSlowLength).LastOrDefault()?.Ema;

        //    if (emaFastData == null || emaSlowData == null)
        //        throw new Exception("Недостаточно данных для рассчета значений EMA");
        //    return ((decimal)emaFastData, (decimal)emaSlowData);
        //}

        /// <summary>
        /// Получает список свечей
        /// </summary>
        /// <param name="count">количество свечей</param>
        //private async Task<List<Candle>> GetLastCandlesAsync(int count)
        //{
        //    var result = await _client.V5Api.ExchangeData.GetKlinesAsync(
        //        category: Category.Spot,
        //        symbol: _symbol,
        //        interval: _candleInterval,
        //        limit: count
        //    );

        //    if (!result.Success || result.Data?.List == null)
        //        throw new Exception($"Ошибка получения свечей: {result.Error}");

        //    return result.Data.List
        //        .Select(k => new Candle
        //        {
        //            DT = k.StartTime,
        //            Open = k.OpenPrice,
        //            High = k.HighPrice,
        //            Low = k.LowPrice,
        //            Close = k.ClosePrice
        //        })
        //        .ToList();
        //}
    }
}
