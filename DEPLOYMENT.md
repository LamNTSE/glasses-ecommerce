# Deployment Guide (Railway / Supabase)

## Required Environment Variables

Set these in Railway project variables:

- `ConnectionStrings__DefaultConnection`
- `Jwt__Key` (at least 32 bytes)
- `Jwt__Issuer`
- `Jwt__Audience`
- `Jwt__AccessTokenExpirationMinutes`
- `Jwt__RefreshTokenExpirationDays`
- `ASPNETCORE_ENVIRONMENT=Production`

If using Supabase PostgreSQL, include SSL in connection string when required, for example:

`Host=...;Port=5432;Database=...;Username=...;Password=...;SSL Mode=Require;Trust Server Certificate=true`

## Behavior on Startup

- App validates `ConnectionStrings:DefaultConnection` and `Jwt:Key` at startup.
- App applies EF Core migrations automatically (`Database:AutoMigrate=true` by default).

## Local Development

- Local values are loaded from `.env` via `DotNetEnv`.
- Keep `.env` out of source control.

## Railway Notes

- Railway injects `PORT`; the app binds it automatically when `ASPNETCORE_URLS` is not set.
- Docker image exposes port `8080`; Railway handles external routing.
