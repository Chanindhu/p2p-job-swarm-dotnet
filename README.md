# P2P Job Swarm for Distributed Computing

A distributed computing prototype built with .NET. The system combines a central ASP.NET Core registry service, a WPF desktop peer client using .NET Remoting, and an ASP.NET Core dashboard for monitoring connected peers and completed jobs.

## Project overview

This repository contains three runnable applications:

| Project | Purpose |
|---|---|
| `Swarm.Server` | ASP.NET Core web service that stores registered clients, heartbeats, job completions, and cleanup state in SQLite. |
| `Swarm.Client` | WPF desktop peer client that hosts a .NET Remoting job board, submits Python jobs, discovers peers, executes jobs, and returns results. |
| `Swarm.Dashboard` | ASP.NET Core MVC dashboard that displays connected clients and completed job counts. |

## Key features

- Central client registry with IP/host, port, display name, and last-seen heartbeat tracking.
- Peer-to-peer job discovery using a registry service plus .NET Remoting between clients.
- WPF client for submitting Python code snippets as distributed jobs.
- IronPython execution engine for running downloaded Python jobs.
- Base64 encoding and SHA-256 hashing for safer job transport and verification.
- SQLite-backed ASP.NET Core API.
- Dashboard refresh support for monitoring connected swarm clients.
- Background cleanup service for stale or offline clients.

## Proof and Validation Evidence

| Evidence | Where to inspect it | What it proves |
|---|---|---|
| Registry API | [`Swarm.Server/`](Swarm.Server/) | The central registration and heartbeat service is implemented. |
| WPF client | [`Swarm.Client/`](Swarm.Client/) | Peer job submission and execution are exposed through a desktop client. |
| Monitoring dashboard | [`Swarm.Dashboard/`](Swarm.Dashboard/) | Connected clients and swarm state can be reviewed visually. |
| API endpoint notes | [`docs/api-endpoints.md`](docs/api-endpoints.md) | The registry endpoints are documented for review and testing. |
| Architecture notes | [`docs/architecture-notes.md`](docs/architecture-notes.md) | The peer-to-peer job flow, hashing, and registry responsibilities are explained. |
| Build and run guide | [`docs/build-and-run.md`](docs/build-and-run.md) | The multi-project .NET solution can be run locally from documented steps. |
| Sample jobs | [`samples/`](samples/) | Reviewers can inspect runnable job payload examples. |
| API collection notes | [`postman/README.md`](postman/README.md) | Registry workflows can be exercised with a prepared API collection. |

## Technology stack

- C#
- ASP.NET Core Web API
- ASP.NET Core MVC
- WPF
- .NET Remoting
- .NET Framework 4.8 client
- .NET 8 server/dashboard
- Entity Framework Core
- SQLite
- IronPython
- RestSharp
- Newtonsoft.Json

## Repository Structure

```text
p2p-job-swarm-dotnet/
|-- JobSwarm.sln
|-- Swarm.Server/                             # Central registry API and SQLite database layer
|-- Swarm.Client/                             # WPF peer client and .NET Remoting job board
|-- Swarm.Dashboard/                          # MVC dashboard for swarm monitoring
|-- docs/
|   |-- api-endpoints.md
|   |-- architecture-notes.md
|   `-- build-and-run.md
|-- samples/                                  # Example Python job snippets
|-- postman/                                  # API testing notes
|-- .gitignore
`-- README.md
```

## Run locally

Open `JobSwarm.sln` in Visual Studio 2022 on Windows, restore NuGet packages, and build the solution.

Start the server first:

```bash
dotnet run --project Swarm.Server/Swarm.Server.csproj --launch-profile http
```

Then start the dashboard:

```bash
dotnet run --project Swarm.Dashboard/Swarm.Dashboard.csproj --launch-profile http
```

Run `Swarm.Client` from Visual Studio because it is a .NET Framework WPF desktop application. Start multiple client instances with different ports, for example `9001`, `9002`, and `9003`.

The default server URL used by the client is:

```text
http://localhost:5265
```

## Notes

- The local SQLite database file is generated at runtime and is intentionally not committed.
- NuGet packages are restored during setup and are intentionally not committed.
- The Python standard library folder used by the WPF client is kept because the client project references it for IronPython support.
