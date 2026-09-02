FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

COPY ["AutoFlow.Web.csproj", "./"]

RUN dotnet restore "AutoFlow.Web.csproj"

COPY . .

RUN dotnet publish "AutoFlow.Web.csproj" \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false


FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final

WORKDIR /app

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://0.0.0.0:10000

COPY --from=build /app/publish .

EXPOSE 10000

ENTRYPOINT ["dotnet", "AutoFlow.Web.dll"]
