FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY Guardiao.sln ./
COPY src/Guardiao.Api/Guardiao.Api.csproj src/Guardiao.Api/
COPY src/Guardiao.Application/Guardiao.Application.csproj src/Guardiao.Application/
COPY src/Guardiao.Domain/Guardiao.Domain.csproj src/Guardiao.Domain/
COPY src/Guardiao.Infrastructure/Guardiao.Infrastructure.csproj src/Guardiao.Infrastructure/
RUN dotnet restore src/Guardiao.Api/Guardiao.Api.csproj
COPY . .
RUN dotnet publish src/Guardiao.Api/Guardiao.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Guardiao.Api.dll"]
