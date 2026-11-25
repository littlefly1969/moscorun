using MoscoRun.Domain.Analysis;

namespace MoscoRun.UnitTests.Domain;

public sealed class CardiacDynamicsTests
{
    [Fact]
    public void Recovery_preserves_signed_change()
    {
        CardiacDynamics.HeartRateRecovery(170, 140).Should().Be(30);
        CardiacDynamics.HeartRateRecovery(150, 160).Should().Be(-10);
        CardiacDynamics.HeartRateRecovery(150, 0).Should().BeNull();
    }

    [Fact]
    public void Hrv_uses_consecutive_intervals()
    {
        var result = CardiacDynamics.SummarizeRr([800, 900, 800]);
        result.Should().NotBeNull();
        result!.RmssdMilliseconds.Should().Be(100);
        result.MeanRrMilliseconds.Should().BeApproximately(833.333333, 0.000001);
        result.SdnnMilliseconds.Should().BeApproximately(57.735027, 0.000001);
    }

    [Fact]
    public void Invalid_interval_is_not_silently_removed()
    {
        CardiacDynamics.SummarizeRr([800, double.NaN, 900, 800]).Should().BeNull();
        CardiacDynamics.SummarizeRr([800, 900]).Should().BeNull();
    }

    [Fact]
    public void Constant_intervals_have_zero_variability()
    {
        var result = CardiacDynamics.SummarizeRr([800, 800, 800]);
        result.Should().NotBeNull();
        result!.RmssdMilliseconds.Should().Be(0);
        result.SdnnMilliseconds.Should().Be(0);
    }

    [Fact]
    public void First_order_response_starts_at_initial_rate()
    {
        CardiacDynamics.FirstOrderResponse(100, 160, 0, 30).Should().Be(100);
        CardiacDynamics.FirstOrderResponse(100, 160, 30, 30)!.Value
            .Should().BeApproximately(137.927234, 0.000001);
        CardiacDynamics.FirstOrderResponse(100, 160, 30, 0).Should().BeNull();
    }
}
