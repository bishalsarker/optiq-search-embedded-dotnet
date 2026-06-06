# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution file
COPY ["src/FlyerBuy.sln", "./"]

# Copy project files
COPY ["src/FlyerBuy.API/FlyerBuy.API.csproj", "FlyerBuy.API/"]
COPY ["src/FlyerBuy.Application/FlyerBuy.Application.csproj", "FlyerBuy.Application/"]
COPY ["src/FlyerBuy.Domain/FlyerBuy.Domain.csproj", "FlyerBuy.Domain/"]
COPY ["src/FlyerBuy.Infrastructure/FlyerBuy.Infrastructure.csproj", "FlyerBuy.Infrastructure/"]
COPY ["src/FlyerBuy.Shared/FlyerBuy.Shared.csproj", "FlyerBuy.Shared/"]

# Restore dependencies
RUN dotnet restore "FlyerBuy.API/FlyerBuy.API.csproj"

# Copy the rest of the code
COPY src/. ./

# Publish the API project
RUN dotnet publish "FlyerBuy.API/FlyerBuy.API.csproj" -c Release -o /app/publish

# Stage 2: Run
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Copy build output
COPY --from=build /app/publish .

# Expose port (update if using a different one)
EXPOSE 80

# ASP.NET will listen on port 80
ENV ASPNETCORE_URLS=http://+:80

# Run the app
ENTRYPOINT ["dotnet", "FlyerBuy.API.dll"]
