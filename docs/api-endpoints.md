# API endpoints

Default server base URL:

```text
http://localhost:5265
```

## Clients

### Register client

```http
POST /api/clients/register
```

Request:

```json
{
  "ipOrHost": "localhost",
  "port": 9001,
  "displayName": "Client 1"
}
```

### Heartbeat

```http
POST /api/clients/heartbeat
```

Request:

```json
{
  "ipOrHost": "localhost",
  "port": 9001
}
```

### Mark offline

```http
POST /api/clients/offline
```

Request:

```json
{
  "ipOrHost": "localhost",
  "port": 9001
}
```

### List clients

```http
GET /api/clients
```

## Jobs

### Record completed job

```http
POST /api/jobs/complete
```

Request:

```json
{
  "clientId": 1,
  "pythonB64": "cHl0aG9uLWpvYi1jb2Rl",
  "sha256Hex": "hash-value",
  "resultB64": "cmVzdWx0",
  "ownerClientId": 2
}
```
