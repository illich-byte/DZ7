# Етап 1: Збірка
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /app

# Копіюємо все (включаючи папки Core та Domain)
COPY . .

# Відновлюємо залежності та збираємо
RUN dotnet restore "WebApiTransfer/WebApiTransfer.csproj"
RUN dotnet publish "WebApiTransfer/WebApiTransfer.csproj" -c Release -o /out /p:UseAppHost=false

# Етап 2: Запуск
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

# Налаштування порту (Koyeb за замовчуванням чекає на 8080 для .NET 9)
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

COPY --from=build /out .

# Створюємо wwwroot для фронтенду та копіюємо index.html
RUN mkdir -p wwwroot
COPY index.html ./wwwroot/index.html

# Запуск (динамічний пошук головної dll)
ENTRYPOINT ["sh", "-c", "dotnet $(ls WebApiTransfer.dll 2>/dev/null || ls *.dll | head -n 1)"]
