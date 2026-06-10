FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY Guardiao.sln ./
COPY src/Guardiao.Worker.Edge/Guardiao.Worker.Edge.csproj src/Guardiao.Worker.Edge/
COPY src/Guardiao.Application/Guardiao.Application.csproj src/Guardiao.Application/
COPY src/Guardiao.Domain/Guardiao.Domain.csproj src/Guardiao.Domain/
COPY src/Guardiao.Infrastructure/Guardiao.Infrastructure.csproj src/Guardiao.Infrastructure/
RUN dotnet restore src/Guardiao.Worker.Edge/Guardiao.Worker.Edge.csproj
COPY . .
RUN dotnet publish src/Guardiao.Worker.Edge/Guardiao.Worker.Edge.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:8.0 AS final
WORKDIR /app
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        curl \
        ffmpeg \
        libfontconfig1 \
        libglib2.0-0 \
        libgl1 \
        libgomp1 \
        libsm6 \
        libx11-6 \
        libxext6 \
        libxrender1 \
    && rm -rf /var/lib/apt/lists/*
COPY --from=build /app/publish .
ENV LD_LIBRARY_PATH=/app/runtimes/linux-x64/native:${LD_LIBRARY_PATH}
ENTRYPOINT ["dotnet", "Guardiao.Worker.Edge.dll"]
