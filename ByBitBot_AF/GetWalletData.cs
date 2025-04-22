using Bybit.Net.Clients;

namespace ByBitBot_AF
{
    public class GetWalletData
    {
        private readonly BybitRestClient _client;
        private readonly string _coin;
        public decimal? TotalEquity { get; set; }                   //общий баланс портфеля в USDT с учетом всех купленных активов
        public decimal? TotalAvailableBalance { get; set; }         //доступный баланс портфеля в USDT
        public decimal? AssetBalance { get; set; }                  //баланс текущей монеты (_coin)
        public decimal? AssetBalanceLocked { get; set; }            //баланс текущей монеты (_coin) заблокированный
        public decimal MinOrderBuyValue { get; set; }               //минимальная цена покупки в USDT
        public decimal MinOrderSellValue { get; set; }              //минимальное количество монет для продажи
        public GetWalletData(BybitRestClient client, string coin)
        {
            _client = client;
            _coin = coin;
        }

        public async Task UpdateWalletData()
        {
            await GetWalletDataAsync();
            await GetMinOrderValues();
        }

        /// <summary>
        /// получаем баланс портфеля
        /// </summary>
        private async Task GetWalletDataAsync()
        {
            var result1 = await _client.V5Api.Account.GetBalancesAsync(Bybit.Net.Enums.AccountType.Unified);
            var assetsInfo = result1.Data.List.FirstOrDefault();

            if (assetsInfo != null)
            {
                TotalEquity = assetsInfo.TotalEquity;
                TotalAvailableBalance = assetsInfo.TotalAvailableBalance;
                foreach (var item in assetsInfo.Assets)
                {
                    if (item.Asset == _coin)
                    {
                        AssetBalance = item.Equity;
                        AssetBalanceLocked = item.Locked;

                    }
                }
            }
        }

        /// <summary>
        /// получаем минимальные значения покупки и продажи
        /// </summary>
        private async Task GetMinOrderValues()
        {
            var result = await _client.V5Api.ExchangeData.GetSpotSymbolsAsync($"{_coin}USDT");
            var symbolInfo = result.Data.List.FirstOrDefault();

            if (symbolInfo != null && symbolInfo.LotSizeFilter != null)
            {
                MinOrderBuyValue = symbolInfo.LotSizeFilter.MinOrderValue;
                MinOrderSellValue = symbolInfo.LotSizeFilter.MinOrderQuantity;
            }
        }



    }
}
