using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Binance.Net.Enums;
using Binance.Net.Interfaces;
using CryptoExchange.Net.Objects;
using CryptoExchange.Net.Sockets;

namespace Blazor.DataProvider
{
    public class BinanceDataProvider
    {
        private IBinanceClient _client;
        private IBinanceSocketClient _socketClient;

        public IBinanceStreamKlineData LastKline { get; private set; }
        public Action<IBinanceStreamKlineData> OnKlineData { get; set; }
        public int? RSI_PERIOD { get; set; }
        public DateTime? KLinesStartTime { get; set; }
        public DateTime? KlinesEndTime { get; set; }

        public BinanceDataProvider(IBinanceClient client, IBinanceSocketClient socketClient)
        {
            _client = client;
            _socketClient = socketClient;
        }

        public async Task<WebCallResult<IEnumerable<IBinanceTick>>> Get24HPrices()
        {
            return await _client.Spot.Market.Get24HPricesAsync();
        }

        public Task<CallResult<UpdateSubscription>> SubscribeTickerUpdates(Action<IEnumerable<IBinanceTick>> tickHandler)
        {
            return _socketClient.Spot.SubscribeToAllSymbolTickerUpdatesAsync(tickHandler);
        }

        public Task<CallResult<UpdateSubscription>> SubscribeToKlineUpdatesAsync(Action<IBinanceStreamKlineData> klineHandler, string symbol = "BTCUSDT", KlineInterval interval = KlineInterval.OneDay)
        {           
            return _socketClient.Spot.SubscribeToKlineUpdatesAsync(symbol, interval, data =>
            {
                LastKline = data;
                OnKlineData?.Invoke(data);
            });
        }

        public async Task<WebCallResult<IEnumerable<IBinanceKline>>> GetKlinesAsync(string symbol, KlineInterval timespan = KlineInterval.OneDay)
        {
            DateTime? startTime = null;
            DateTime? endTime = null;
            int? maxResults = RSI_PERIOD;
            return await _client.Spot.Market.GetKlinesAsync(symbol, timespan, startTime, endTime, maxResults, System.Threading.CancellationToken.None);
        }

        public async Task Unsubscribe(UpdateSubscription subscription)
        {
            await _socketClient.Unsubscribe(subscription);
        }
    }
}
