using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Binance.Net.Interfaces;
using CryptoExchange.Net.Objects;
using CryptoExchange.Net.Sockets;

namespace Blazor.DataProvider
{
    public class BinanceDataProvider
    {
        private IBinanceClient _client;
        private IBinanceSocketClient _socketClient;

        public BinanceDataProvider(IBinanceClient client, IBinanceSocketClient socketClient)
        {
            _client = client;
            _socketClient = socketClient;
        }

        //public Task<WebCallResult<IEnumerable<IBinanceTick>>> Get24HPrices()
        //{
        //    return _client.Spot.Market.Get24HPricesAsync();
        //}

        public async Task<WebCallResult<IEnumerable<IBinanceTick>>> Get24HPrices()
        {
            return await _client.Spot.Market.Get24HPricesAsync();
        }

        public Task<CallResult<UpdateSubscription>> SubscribeTickerUpdates(Action<IEnumerable<IBinanceTick>> tickHandler)
        {
            return _socketClient.Spot.SubscribeToAllSymbolTickerUpdatesAsync(tickHandler);
        }

        public async Task<WebCallResult<IEnumerable<IBinanceKline>>> GetKlinesAsync(string symbol)
        {
            Binance.Net.Enums.KlineInterval stimespan = Binance.Net.Enums.KlineInterval.OneMinute;
            DateTime? startTime = null;
            DateTime? endTime = null;
            int? maxResults = 14;
            return await _client.Spot.Market.GetKlinesAsync(symbol, stimespan, startTime, endTime, maxResults, System.Threading.CancellationToken.None);
        }

        public async Task Unsubscribe(UpdateSubscription subscription)
        {
            await _socketClient.Unsubscribe(subscription);
        }
    }
}
