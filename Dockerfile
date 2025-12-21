# Використовуємо SDK версії 9.0 для збірки
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Копіюємо файли проєктів
COPY ["WebApiTransfer/WebApiTransfer.csproj", "WebApiTransfer/"]
COPY ["Core/Core.csproj", "Core/"]
COPY ["Domain/Domain.csproj", "Domain/"]

# Відновлюємо залежності (тепер SDK 9.0 зрозуміє ваші проєкти)
RUN dotnet restore "WebApiTransfer/WebApiTransfer.csproj"

# Копіюємо решту коду
COPY . .

# Збираємо
WORKDIR "/src/WebApiTransfer"
RUN dotnet build "WebApiTransfer.csproj" -c Release -o /app/build

# Публікуємо
FROM build AS publish
RUN dotnet publish "WebApiTransfer.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Використовуємо Runtime версії 9.0 для запуску
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
EXPOSE 8080

# ВАЖЛИВО для .NET 9: вказуємо порт
ENV ASPNETCORE_URLS=http://+:8080

COPY --from=publish /app/publish .

# Копіюємо фронтенд
COPY --from=build /src/index.html ./wwwroot/index.html

ENTRYPOINT ["dotnet", "WebApiTransfer.dll"]
