# Build and run guide

## Requirements

- Windows
- Visual Studio 2022
- .NET 8 SDK
- .NET Framework 4.8 Developer Pack
- NuGet package restore enabled

## Build

Open `JobSwarm.sln` in Visual Studio and choose **Restore NuGet Packages**, then **Build Solution**.

The web projects can also be restored from the terminal:

```bash
dotnet restore Swarm.Server/Swarm.Server.csproj
dotnet restore Swarm.Dashboard/Swarm.Dashboard.csproj
```

The WPF client is a .NET Framework project, so Visual Studio is the safest build path.

## Run order

### 1. Start the registry API

```bash
dotnet run --project Swarm.Server/Swarm.Server.csproj --launch-profile http
```

Default URL:

```text
http://localhost:5265
```

Swagger opens at:

```text
http://localhost:5265/swagger
```

### 2. Start the dashboard

```bash
dotnet run --project Swarm.Dashboard/Swarm.Dashboard.csproj --launch-profile http
```

Default URL:

```text
http://localhost:5132
```

### 3. Start peer clients

Run `Swarm.Client` from Visual Studio. Use a different listening port for each instance.

Example local clients:

```text
Client 1: localhost:9001
Client 2: localhost:9002
Client 3: localhost:9003
```

Submit a Python job in one client, then allow another peer to discover and execute it.
