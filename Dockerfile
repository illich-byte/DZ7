# Етап збірки (SDK)
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Копіюємо файли проектів для відновлення залежностей
COPY ["WebApiTransfer/WebApiTransfer.csproj", "WebApiTransfer/"]
COPY ["Core/Core.csproj", "Core/"]
COPY ["Domain/Domain.csproj", "Domain/"]

# Відновлюємо NuGet пакети
RUN dotnet restore "WebApiTransfer/WebApiTransfer.csproj"

# Копіюємо весь інший код
COPY . .

# Збираємо основний проект
WORKDIR "/src/WebApiTransfer"
RUN dotnet build "WebApiTransfer.csproj" -c Release -o /app/build

# Публікуємо проект
FROM build AS publish
RUN dotnet publish "WebApiTransfer.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Фінальний образ (Runtime)
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
# Важливо: .NET 8 за замовчуванням використовує порт 8080
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

COPY --from=publish /app/publish .

# Якщо хочете, щоб index.html був доступний через бекенд, 
# він має лежати в папці wwwroot (якщо бекенд налаштований на static files)
COPY ../index.html ./wwwroot/ 

ENTRYPOINT ["dotnet", "WebApiTransfer.dll"]
