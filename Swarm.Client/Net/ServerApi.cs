using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using RestSharp;

namespace Swarm.Client.Net
{
    public class ServerApi
    {
        private readonly RestClient _client;

        public ServerApi(string baseUrl)
        {
            _client = new RestClient(baseUrl.TrimEnd('/'));
        }

        public class RegisterDto
        {
            public string IpOrHost { get; set; }
            public int Port { get; set; }
            public string DisplayName { get; set; }
            public RegisterDto(string ipOrHost, int port, string displayName)
            { IpOrHost = ipOrHost; Port = port; DisplayName = displayName; }
        }

        public class ClientDto
        {
            public int Id { get; set; }
            public string IpOrHost { get; set; }
            public int Port { get; set; }
            public string DisplayName { get; set; }
            public DateTime LastSeenUtc { get; set; }
            public int TotalJobsDone { get; set; }
            public bool IsOnline { get; set; }   // server may send this
        }

        public class CompleteDto
        {
            public int ClientId { get; set; }
            public string PythonB64 { get; set; }
            public string Sha256Hex { get; set; }
            public string ResultB64 { get; set; }
            public int? OwnerClientId { get; set; }
            public CompleteDto(int clientId, string pythonB64, string sha256Hex, string resultB64, int? ownerClientId)
            { ClientId = clientId; PythonB64 = pythonB64; Sha256Hex = sha256Hex; ResultB64 = resultB64; OwnerClientId = ownerClientId; }
        }

        public class HeartbeatDto
        {
            public string IpOrHost { get; set; }
            public int Port { get; set; }
            public HeartbeatDto(string host, int port) { IpOrHost = host; Port = port; }
        }

        public class OfflineDto
        {
            public string IpOrHost { get; set; }
            public int Port { get; set; }
            public OfflineDto(string host, int port) { IpOrHost = host; Port = port; }
        }

        public async Task<bool> HeartbeatAsync(string host, int port)
        {
            var req = new RestRequest("/api/clients/heartbeat", Method.Post)
                .AddJsonBody(new HeartbeatDto(host, port));
            var resp = await _client.ExecuteAsync(req);
            return resp.IsSuccessful;
        }

        public async Task<bool> OfflineAsync(string host, int port)
        {
            var req = new RestRequest("/api/clients/offline", Method.Post)
                .AddJsonBody(new OfflineDto(host, port));
            var resp = await _client.ExecuteAsync(req);
            return resp.IsSuccessful;
        }

        public async Task<bool> RegisterAsync(string ipOrHost, int port, string displayName)
        {
            var req = new RestRequest("/api/clients/register", Method.Post)
                .AddJsonBody(new RegisterDto(ipOrHost, port, displayName));
            var resp = await _client.ExecuteAsync(req);
            return resp.IsSuccessful;
        }

        public async Task<List<ClientDto>> ListClientsAsync()
        {
            var req = new RestRequest("/api/clients", Method.Get);
            var resp = await _client.ExecuteAsync(req);
            if (!resp.IsSuccessful || string.IsNullOrEmpty(resp.Content))
                return new List<ClientDto>();
            return JsonConvert.DeserializeObject<List<ClientDto>>(resp.Content) ?? new List<ClientDto>();
        }

        public async Task<bool> CompleteAsync(int clientId, string pythonB64, string sha256Hex, string resultB64, int? ownerClientId)
        {
            var req = new RestRequest("/api/jobs/complete", Method.Post)
                .AddJsonBody(new CompleteDto(clientId, pythonB64, sha256Hex, resultB64, ownerClientId));
            var resp = await _client.ExecuteAsync(req);
            return resp.IsSuccessful;
        }
    }
}
