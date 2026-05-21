using Api.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Api.Hubs;

/// <summary>
/// SignalR hub for real-time queue push events (us_033/AC-001, AC-002, AC-005).
///
/// <para>
/// No client-callable hub methods are defined — this hub is used exclusively for
/// server-initiated broadcasts via <see cref="IHubContext{QueueHub}"/> injected into
/// <c>QueueHubService</c>.
/// </para>
/// <para>
/// The <see cref="AuthorizeAttribute"/> causes the SignalR handshake to fail with HTTP 401
/// (unauthenticated) or HTTP 403 (wrong role) before the connection is established (AC-005; OWASP A01).
/// </para>
/// </summary>
[Authorize(Roles = $"{Roles.Staff},{Roles.Admin}")]
public sealed class QueueHub : Hub { }
