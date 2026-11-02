# Образ .NET 10 API (сборка: docker compose build api)
# Двухэтапная сборка: SDK компилирует, в итоговом образе — только рантайм ASP.NET и опубликованные файлы.

# --- 1. Сборка ---
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Сначала только .csproj — слой с NuGet-пакетами кешируется, пока не меняются зависимости
COPY HealthlyApi.csproj ./
RUN dotnet restore

COPY . ./
RUN dotnet publish -c Release -o /app/publish --no-restore

# --- 2. Рантайм ---
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Tesseract OCR с английским, русским и турецким — распознаёт фото и сканы бланков локально (Services/Ai/Ocr).
# Ocr:TesseractPath по умолчанию "tesseract" — программа из PATH, языки — в её стандартной папке, настраивать ничего не нужно
RUN apt-get update \
    && apt-get install -y --no-install-recommends tesseract-ocr tesseract-ocr-eng tesseract-ocr-rus tesseract-ocr-tur \
    && rm -rf /var/lib/apt/lists/*

# Папка для загруженных документов; к ней подключается том uploads (см. docker-compose.yml).
# Владелец — непривилегированный пользователь app из базового образа, иначе API не сможет писать в том.
RUN mkdir -p /app/uploads && chown "$APP_UID" /app/uploads

COPY --from=build /app/publish ./

ENV ASPNETCORE_HTTP_PORTS=8080 \
    Storage__UploadsPath=/app/uploads

EXPOSE 8080
USER $APP_UID

ENTRYPOINT ["dotnet", "HealthlyApi.dll"]
