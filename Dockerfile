FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["PericonAPI.csproj", "./"]
RUN dotnet restore "PericonAPI.csproj"

COPY . .
RUN dotnet publish "PericonAPI.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
COPY pericon.db ./pericon.db

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "PericonAPI.dll"]
