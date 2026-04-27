# Deployment Guide (Railway / Supabase)

## Railway Environment Variables

Set these in Railway project variables before deploying:

- `ASPNETCORE_ENVIRONMENT=Production`
- `ConnectionStrings__DefaultConnection`
- `Jwt__Key` (at least 32 bytes)
- `Jwt__Issuer`
- `Jwt__Audience`
- `Jwt__AccessTokenExpirationMinutes` (optional, defaults to 180)
- `Jwt__RefreshTokenExpirationDays` (optional, defaults to 30)
- `Vnpay__TmnCode`
- `Vnpay__HashSecret`
- `Vnpay__Url`
- `Vnpay__ReturnUrl`
- `Vnpay__IpnUrl`
- `Vnpay__FrontendBaseUrl`
- `OpenAI__ApiKey` if chatbot features need OpenAI access

Recommended PostgreSQL connection string format for Railway or Supabase:

`Host=...;Port=5432;Database=...;Username=...;Password=...;SSL Mode=Require;Trust Server Certificate=true`

## Startup Behavior

- The app fails fast on startup if `ConnectionStrings:DefaultConnection` is missing.
- The app fails fast on startup if `Jwt:Key`, `Jwt:Issuer`, or `Jwt:Audience` is missing or invalid.
- The app fails fast on startup if any required `Vnpay` setting is missing or if the URLs are not absolute.
- The current code does not apply EF Core migrations automatically at startup.

## Local Development

- Local values are loaded from `.env` via `DotNetEnv` only when the file exists.
- Keep `.env` out of source control.

## Railway Notes

- Railway injects `PORT`; the app binds to it automatically in production.
- The Docker image exposes port `8080`; Railway routes traffic to the container port.
- If you use a fresh database, make sure the schema exists before the first request hits the API.
