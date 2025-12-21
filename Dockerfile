# Етап 1: Збірка
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /app

# Копіюємо все (це надійніше для багатопроєктних рішень)
COPY . .

# Знаходимо будь-який файл .csproj у папці WebApiTransfer та публікуємо його
RUN dotnet publish WebApiTransfer/*.csproj -c Release -o /out /p:UseAppHost=false

# Етап 2: Запуск
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

# Копіюємо результат збірки
COPY --from=build /out .

# Створюємо папку для фронтенду та копіюємо index.html
RUN mkdir -p wwwroot
COPY --from=build /app/index.html ./wwwroot/index.html

# Автоматичний запуск: запускаємо файл, назва якого збігається з проектом
ENTRYPOINT ["sh", "-c", "dotnet $(ls *.dll | head -n 1)"]
