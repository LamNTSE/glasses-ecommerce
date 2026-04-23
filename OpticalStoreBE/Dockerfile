FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY OpticalStoreBE/OpticalStoreBE.sln ./

COPY OpticalStoreBE/OpticalStore.API/OpticalStore.API.csproj OpticalStore.API/
COPY OpticalStoreBE/OpticalStore.BLL/OpticalStore.BLL.csproj OpticalStore.BLL/
COPY OpticalStoreBE/OpticalStore.DAL/OpticalStore.DAL.csproj OpticalStore.DAL/

RUN dotnet restore OpticalStoreBE.sln

COPY OpticalStoreBE/ .

RUN dotnet publish OpticalStore.API/OpticalStore.API.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "OpticalStore.API.dll"]