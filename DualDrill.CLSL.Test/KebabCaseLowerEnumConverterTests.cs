using System.Text.Json;
using System.Text.Json.Serialization;
using DualDrill.Common;

namespace DualDrill.CLSL.Test;

public sealed class KebabCaseLowerEnumConverterTests
{
    [JsonConverter(typeof(KebabCaseLowerEnumConverter<Binding>))]
    private enum Binding { ReadOnlyStorage, WriteOnlyStorage }

    [Fact]
    public void ReadsAndWritesKebabCaseAndRejectsInvalidInput()
    {
        Assert.Equal("\"read-only-storage\"", JsonSerializer.Serialize(Binding.ReadOnlyStorage));
        Assert.Equal(Binding.ReadOnlyStorage, JsonSerializer.Deserialize<Binding>("\"read-only-storage\""));
        Assert.Equal(Binding.WriteOnlyStorage, JsonSerializer.Deserialize<Binding>("\"WriteOnlyStorage\""));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Binding>("null"));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Binding>("\"unknown\""));
    }
}
