FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY SaBai.ServiceDefaults/SaBai.ServiceDefaults.csproj SaBai.ServiceDefaults/
COPY SaBai.API/SaBai.API.csproj SaBai.API/
RUN dotnet restore SaBai.API/SaBai.API.csproj
COPY SaBai.ServiceDefaults/ SaBai.ServiceDefaults/
COPY SaBai.API/ SaBai.API/
RUN dotnet publish SaBai.API/SaBai.API.csproj -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "SaBai.API.dll"]
