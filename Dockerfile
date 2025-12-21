# Етап 1: SDK для збірки
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Копіюємо файли проектів (ВАЖЛИВО: назви мають точно збігатися з репозиторієм)
COPY ["WebApiTransfer/WebApiTransfer.csproj", "WebApiTransfer/"]
COPY ["Core/Core.csproj", "Core/"]
COPY ["Domain/Domain.csproj", "Domain/"]

# Відновлюємо залежності
RUN dotnet restore "WebApiTransfer/WebApiTransfer.csproj"

# Копіюємо решту файлів (включаючи index.html з кореня)
COPY . .

# Збираємо проект
WORKDIR "/src/WebApiTransfer"
RUN dotnet build "WebApiTransfer.csproj" -c Release -o /app/build

# Публікуємо
FROM build AS publish
RUN dotnet publish "WebApiTransfer.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Етап 2: Runtime (фінальний образ)
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 8080

# Встановлюємо порт для .NET
ENV ASPNETCORE_URLS=http://+:8080

# Копіюємо зібраний бекенд
COPY --from=publish /app/publish .

# Копіюємо ваш index.html (фронтенд) у папку для статичних файлів
# (Він лежить у корені /src, тому використовуємо шлях із етапу build)
COPY --from=build /src/index.html ./wwwroot/index.html

ENTRYPOINT ["dotnet", "WebApiTransfer.dll"]
