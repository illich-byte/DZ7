# Використовуємо SDK 9.0 для збірки
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /app

# Копіюємо ВЕСЬ вміст репозиторію відразу
# Це гарантує, що всі папки (Core, Domain, WebApiTransfer) будуть на місці
COPY . .

# Відновлюємо залежності та публікуємо проєкт
# Ми вказуємо шлях до .csproj файлу відносно кореня
RUN dotnet publish "WebApiTransfer/WebApiTransfer.csproj" -c Release -o /out /p:UseAppHost=false

# Створюємо фінальний образ
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

# Копіюємо зібрані файли з етапу збірки
COPY --from=build /out .

# Копіюємо ваш index.html (він у корені) у папку wwwroot для фронтенду
# Створюємо папку, якщо її немає, і копіюємо файл
RUN mkdir -p wwwroot
COPY --from=build /app/index.html ./wwwroot/index.html

ENTRYPOINT ["dotnet", "WebApiTransfer.dll"]
