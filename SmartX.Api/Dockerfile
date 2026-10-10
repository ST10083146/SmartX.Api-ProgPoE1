# ---- build stage ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY SmartX.Api.csproj ./
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /app/publish

# ---- run stage ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "SmartX.Api.dll"]
