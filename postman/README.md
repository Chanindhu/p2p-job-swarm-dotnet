# Postman testing

Use the endpoint examples in `docs/api-endpoints.md` to test the registry service.

Recommended test order:

1. Start `Swarm.Server`.
2. `POST /api/clients/register` for one or more clients.
3. `GET /api/clients` to verify registration.
4. `POST /api/clients/heartbeat` to keep a client online.
5. `POST /api/jobs/complete` to record completed work.
6. Open the dashboard and confirm the state is visible.
