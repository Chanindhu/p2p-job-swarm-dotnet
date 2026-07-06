# Architecture notes

The system uses a hybrid distributed computing architecture.

## Components

### Swarm.Server

The server is not responsible for executing jobs. It acts as a central registry that allows peers to find each other. Clients register their host/port, send heartbeats, and post completed job information. Data is stored through Entity Framework Core using SQLite.

### Swarm.Client

Each desktop client has two responsibilities:

1. Host a .NET Remoting job board so other peers can pull work.
2. Run a background networking loop that discovers peers, pulls jobs, executes Python code through IronPython, and submits results back to the job owner.

The WPF UI is used to configure the server endpoint, client port, Python job code, and local status.

### Swarm.Dashboard

The dashboard communicates with the server API and presents a monitoring view of connected clients and completed jobs.

## Main flow

1. Client starts and hosts a .NET Remoting endpoint.
2. Client registers with the central API server.
3. Client periodically sends heartbeats.
4. Client polls the registry to discover other peers.
5. Client contacts other peers over .NET Remoting.
6. Client downloads Python jobs and verifies the SHA-256 hash.
7. Client executes the job using IronPython.
8. Client returns the result to the owner peer and posts completion metadata to the server.
9. Dashboard displays live client and job state from the server.

## Safety and validation

- Base64 is used for Python code transport.
- SHA-256 hashes are used to verify job payload integrity.
- Client cleanup marks stale clients as offline based on last-seen timestamps.
- Exceptions are handled around networking, remoting, and Python execution paths.
