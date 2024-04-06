using Drill.Connection;
using Drilla.Server;
using Microsoft.AspNetCore.Mvc;
using SIPSorcery.Media;
using SIPSorcery.Net;
using SIPSorceryMedia.Encoders;
using System.Text;


var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddSignalR();
//builder.Services.AddSingleton<RTCClientsStore>();
builder.Services.AddConnectionServices();

builder.Services.AddHealthChecks();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.

app.UseDrillClients();

//app.MapPost("/api/render/push", async (HttpRequest request, CancellationToken cancel, [FromServices] RTCClientsStore clients) =>
//{
//    using var reader = new StreamReader(request.Body, Encoding.UTF8);
//    var sdp = await reader.ReadToEndAsync(cancel).ConfigureAwait(false);

//    var pc = new RTCPeerConnection(null);
//    pc.setRemoteDescription(
//        new RTCSessionDescriptionInit
//        {
//            type = RTCSdpType.offer,
//            sdp = sdp
//        });

//    var ans = pc.createAnswer();
//    await pc.setLocalDescription(ans).ConfigureAwait(false);
//    var _ = new RTCPushClientConnection(pc, (channel) =>
//    {
//        if (clients.PushChannel is null)
//        {
//            clients.PushChannel = channel;
//            clients.TryLinkClients();
//        }
//    });
//    return ans.sdp;
//});

//app.MapPost("/api/render/pull", async (HttpRequest request, CancellationToken cancel, [FromServices] RTCClientsStore clients) =>
//{
//    using var reader = new StreamReader(request.Body, Encoding.UTF8);
//    var sdp = await reader.ReadToEndAsync(cancel).ConfigureAwait(false);

//    var pc = new RTCPeerConnection(null);
//    pc.setRemoteDescription(
//        new RTCSessionDescriptionInit
//        {
//            type = RTCSdpType.offer,
//            sdp = sdp
//        });

//    var ans = pc.createAnswer();
//    await pc.setLocalDescription(ans).ConfigureAwait(false);
//    var _ = new RTCPushClientConnection(pc, (channel) =>
//    {
//        if (clients.PullChannel is null)
//        {
//            clients.PullChannel = channel;
//            clients.TryLinkClients();
//        }
//    });
//    return ans.sdp;
//});


if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseHealthChecks("/health");

app.MapFallbackToFile("/index.html");

app.Run();

