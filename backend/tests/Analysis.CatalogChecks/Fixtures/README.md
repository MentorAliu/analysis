# Offline M2 contract fixtures

These fixtures are documentation examples and explicit synthetic variants. They are
not captured observations, current asset coverage, backtest inputs or product data.
Tests may persist them only in a disposable database. Nothing loads fixtures in the
API/worker's normal startup or production frontend.

Sources checked 2026-09-05:

- `binance-documented.json`: numeric response example from the official
  [Spot REST documentation](https://github.com/binance/binance-spot-api-docs/blob/master/rest-api.md#klinecandlestick-data).
  Whitespace/comments removed. Its example close timestamp does not describe a 1h
  candle; the 1h adapter must reject it.
- `binance-hour-variant.json`: **synthetic** derivative of that example; only open
  and close timestamps changed to 2021-01-01 00:00Z and 00:59:59.999Z. Test request
  context binds it to each BTC/ETH/SOL spot instrument. Those bindings are synthetic.
- `bybit-funding-documented.json`: official
  [funding response example](https://bybit-exchange.github.io/docs/v5/market/history-fund-rate),
  formatting removed. The documented ETHPERP identity must not silently map to ETHUSDT.
- `bybit-oi-documented.json`: official
  [OI response example](https://bybit-exchange.github.io/docs/v5/market/open-interest),
  formatting removed. It is **inverse BTCUSD**, so the selected linear adapter rejects it.
- `defillama-schema-example.json`: one array item assembled from the `date` and
  `tvl` example literals in the official
  [free OpenAPI schema](https://github.com/DefiLlama/api-docs/blob/main/defillama-openapi-free.json).
  These generic schema examples are not a historical Ethereum or Solana measurement.

`FixtureServer.cs` constructs **synthetic contract variants** from these schemas:
BTCUSDT/ETHUSDT/SOLUSDT identities, UTC 2021-01-01 sample times, OI 12.12345678,
funding 0.0001, and instrument metadata. Extra/missing fields, altered values,
pagination/cursor responses and HTTP failures are test-only mutations. They prove
mapping behavior and explicitly do not prove live provider coverage or data rights.
