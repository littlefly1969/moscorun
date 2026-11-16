using System.Net;
using System.Net.Http.Json;
using MoscoRun.Api.Features.Devices;
using MoscoRun.Domain.Devices;

namespace MoscoRun.IntegrationTests;

public sealed class DeviceIdentityTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Device_token_identifies_its_device_and_account()
    {
        var (user, userId) = await factory.CreateUserClientAsync();
        var (phone, registered) = await factory.CreateDeviceClientAsync(user, "Telefono di prova", DeviceKind.PhoneBridge);

        var identity = await phone.GetFromJsonAsync<DeviceIdentityDto>("/api/v1/device", Json.Options, Ct);

        identity.Should().Be(new DeviceIdentityDto(registered.Device.Id, "Telefono di prova", DeviceKind.PhoneBridge, userId));
    }

    [Fact]
    public async Task Two_devices_of_the_same_person_share_the_account_and_other_people_do_not()
    {
        var (user, _) = await factory.CreateUserClientAsync();
        var (first, _) = await factory.CreateDeviceClientAsync(user, "Primo", DeviceKind.PhoneBridge);
        var (second, _) = await factory.CreateDeviceClientAsync(user, "Secondo", DeviceKind.ScaleAgent);
        var (otherUser, _) = await factory.CreateUserClientAsync();
        var (other, _) = await factory.CreateDeviceClientAsync(otherUser, "Altro", DeviceKind.PhoneBridge);

        var a = (await first.GetFromJsonAsync<DeviceIdentityDto>("/api/v1/device", Json.Options, Ct))!;
        var b = (await second.GetFromJsonAsync<DeviceIdentityDto>("/api/v1/device", Json.Options, Ct))!;
        var c = (await other.GetFromJsonAsync<DeviceIdentityDto>("/api/v1/device", Json.Options, Ct))!;

        b.AccountId.Should().Be(a.AccountId);
        b.DeviceId.Should().NotBe(a.DeviceId);
        c.AccountId.Should().NotBe(a.AccountId);
    }

    [Fact]
    public async Task Identity_requires_a_valid_device_token()
    {
        var (user, _) = await factory.CreateUserClientAsync();
        var (phone, registered) = await factory.CreateDeviceClientAsync(user, "Telefono", DeviceKind.PhoneBridge);

        (await user.GetAsync("/api/v1/device", Ct)).StatusCode.Should().Be(HttpStatusCode.Unauthorized, "la sessione web non è un dispositivo");
        (await user.DeleteAsync($"/api/v1/devices/{registered.Device.Id}", Ct)).EnsureSuccessStatusCode();
        (await phone.GetAsync("/api/v1/device", Ct)).StatusCode.Should().Be(HttpStatusCode.Unauthorized, "token revocato");
    }
}
