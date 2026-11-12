FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY nuget.config ./
COPY src/BonusTest.Domain/BonusTest.Domain.csproj src/BonusTest.Domain/
COPY src/BonusTest.Application/BonusTest.Application.csproj src/BonusTest.Application/
COPY src/BonusTest.Infrastructure/BonusTest.Infrastructure.csproj src/BonusTest.Infrastructure/
COPY src/BonusTest.Api/BonusTest.Api.csproj src/BonusTest.Api/
RUN dotnet restore src/BonusTest.Api/BonusTest.Api.csproj
COPY src/ src/
RUN dotnet publish src/BonusTest.Api/BonusTest.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "BonusTest.Api.dll"]
