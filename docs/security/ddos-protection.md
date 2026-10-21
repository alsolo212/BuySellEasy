# DDoS protection

The API has application-level flood protection enabled by default:

- Every request is limited per client IP to a sustained 300 requests per minute, with a short initial burst of 60 requests. Rejected requests receive `429 Too Many Requests` and, when available, `Retry-After`.
- Existing stricter endpoint limits continue to apply to authentication, uploads, and the support bot.
- Kestrel rejects excessive concurrent connections, limits upgraded (SignalR) connections, enforces header and keep-alive timeouts, requires a minimum upload rate, and caps request bodies at 32 MiB.

The limits are configured in `Api/appsettings.json` under `DdosProtection`. Tune them from production configuration rather than changing source code. Values are clamped to safe ranges by the application.

## Production edge protection (required for volumetric attacks)

Application limits cannot protect the server's internet connection from a large Layer 3/4 attack. Before exposing the service publicly, put both the frontend and API behind a CDN/WAF such as Cloudflare and:

1. Proxy the public DNS records through the CDN/WAF and enable its managed WAF rules and DDoS protection.
2. Permit traffic to the origin only from the CDN/WAF or from a private load balancer. Do not expose the origin IP through DNS, direct ports, or mail records.
3. Cache static frontend assets and public listing images at the edge; do not cache authenticated API responses.
4. Add a CDN/WAF rate rule for `/api/auth/*` and another for high-cost endpoints such as uploads and support-chat messages. Start from the API limits above and tune from real traffic.
5. Monitor edge and origin `429`, connection, bandwidth, and error-rate metrics; alert on abrupt changes.

## Reverse proxy client IPs

Rate limits use the TCP peer address by default. If the API is behind a reverse proxy, configure only the IP addresses of the *immediate, trusted* proxies:

```json
"ReverseProxy": {
  "TrustedProxies": ["10.0.0.10"]
}
```

The API then accepts a single `X-Forwarded-For` hop from those proxies. Never add public client ranges or `0.0.0.0`; that would allow callers to forge an IP and bypass per-client limits. With no configured proxy, ASP.NET Core's loopback-only default is retained.
