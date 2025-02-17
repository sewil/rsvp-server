FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build

WORKDIR /app

# Copy files to image
COPY ./rsvp-server .

# Build game
WORKDIR /app/WvsBeta.Game
RUN dotnet build -o /app/BinSvr

# Run
FROM mcr.microsoft.com/dotnet/runtime:9.0
WORKDIR /app

# Copy BinSvr
COPY --from=build /app/BinSvr ./BinSvr

# Copy data
COPY ./rsvp-data ./DataSvr
COPY ./rsvp-server/DataSvr ./DataSvr
COPY ./rsvp-server/WvsBeta.Database/evolutions ./BinSvr/evolutions

COPY ./rsvp-server/entrypoint.sh .
ENTRYPOINT ["/app/entrypoint.sh"]

CMD ["/shared/BinSvr/WvsBeta.Game", "Game0"]
