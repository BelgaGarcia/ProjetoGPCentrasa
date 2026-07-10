using CentraSA.Domain.Rules;

namespace CentraSA.UnitTests;

public sealed class CodeNormalizerTests
{
    [Theory]
    [InlineData("SMUD84", "SMUD084")]
    [InlineData("smud-081", "SMUD081")]
    [InlineData(" SMUD 85 ", "SMUD085")]
    [InlineData("SMUD1001", "SMUD1001")]
    public void SmudCodeIsCanonicalized(string input, string expected)
    {
        Assert.Equal(expected, SmudCodeNormalizer.Normalize(input));
    }

    [Theory]
    [InlineData("84")]
    [InlineData("SMUD")]
    [InlineData("SMUDABC")]
    [InlineData("SMUD000")]
    public void InvalidSmudCodeIsRejected(string input)
    {
        Assert.Throws<FormatException>(() => SmudCodeNormalizer.Normalize(input));
    }

    [Fact]
    public void TicketNumberAcceptsOnlyDigits()
    {
        Assert.Equal("14779", TicketNumberNormalizer.Normalize(" 14779 "));
        Assert.Throws<FormatException>(() => TicketNumberNormalizer.Normalize("CH-14779"));
    }
}
