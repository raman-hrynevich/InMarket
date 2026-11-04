FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["NotificationService/NotificationService.csproj", "NotificationService/"]
COPY ["NotificationService.Application/NotificationService.Application.csproj", "NotificationService.Application/"]
COPY ["NotificationService.Domain/NotificationService.Domain.csproj", "NotificationService.Domain/"]
COPY ["NotificationService.Infrastructure/NotificationService.Infrastructure.csproj", "NotificationService.Infrastructure/"]

RUN dotnet restore "NotificationService/NotificationService.csproj"

COPY . .

WORKDIR /src/NotificationService
RUN dotnet publish "NotificationService.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 8080

USER app

ENTRYPOINT ["dotnet", "NotificationService.dll"]
