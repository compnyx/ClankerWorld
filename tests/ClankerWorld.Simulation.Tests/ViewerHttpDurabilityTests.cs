using System.Net;
using System.Security.Cryptography;
using ClankerWorld.Simulation.Playtest;
using ClankerWorld.Viewer.Control;
using ClankerWorld.Viewer.Observation;
using Microsoft.Extensions.DependencyInjection;

namespace ClankerWorld.Simulation.Tests;

public sealed partial class ViewerHttpTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SuccessfulMutationRetryPersistsAnAlreadyChangedRuntime(bool rename)
    {
        var directory = Directory.CreateTempSubdirectory("durable-owner-retry-");
        try
        {
            using var host = new ViewerWebApplicationFactory(directory.FullName, privateWorld: true);
            using var client = host.CreateClient();
            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var device = await StartAndActivateAsync(host, client, key);
            var runtime = host.Services.GetRequiredService<PrivateWorldRuntime>();
            var file = host.Services.GetRequiredService<PrivateWorldStateFile>();
            file.Save(runtime);
            var savedPath = file.Path + ".prior";
            File.Move(file.Path, savedPath);
            Directory.CreateDirectory(file.Path);
            async Task<HttpResponseMessage> SendMutation()
            {
                if (rename)
                {
                    var action = new OwnerAgentRenameAction("founder-scout", "Durable Name");
                    return await SendSignedAsync(host, client, key, device.DeviceId,
                        "/api/v1/owner/agents/rename", action, OwnerHttpBinding.AgentRenamePayload(action));
                }
                return await SendSignedAsync(host, client, key, device.DeviceId,
                    "/api/v1/owner/control/pause", new OwnerControlAction("pause"), OwnerHttpBinding.EmptyPayload("pause"));
            }
            try
            {
                using var failed = await SendMutation();
                Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            Assert.True(rename ? runtime.Society.GetInhabitant("founder-scout").Name == "Durable Name" : runtime.Society.IsPaused);
            Directory.Delete(file.Path);
            File.Move(savedPath, file.Path);
            using var retry = await SendMutation();
            Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
            using var restored = file.LoadOrCreate(runtime.ExportState().WorldSeed);
            Assert.True(rename ? restored.Society.GetInhabitant("founder-scout").Name == "Durable Name" : restored.Society.IsPaused);
        }
        finally { directory.Delete(recursive: true); }
    }
}
