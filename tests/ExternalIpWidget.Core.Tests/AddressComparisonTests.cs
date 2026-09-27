using ExternalIpWidget.Core;

namespace ExternalIpWidget.Core.Tests;

public class AddressComparisonTests
{
    [Fact]
    public void Explains_a_missing_nic_address()
    {
        Assert.Equal(AddressComparison.NicUnavailable, AddressComparison.Describe("203.0.113.10", null));
    }

    [Fact]
    public void Explains_a_direct_connection()
    {
        Assert.Equal(AddressComparison.Same, AddressComparison.Describe("203.0.113.10", "203.0.113.10"));
    }

    [Fact]
    public void Explains_nat_when_the_nic_is_local()
    {
        Assert.Equal(AddressComparison.BehindNat, AddressComparison.Describe("203.0.113.10", "192.168.1.20"));
        Assert.Equal(AddressComparison.BehindNat, AddressComparison.Describe("203.0.113.10", "100.64.1.5"));
    }

    [Fact]
    public void Explains_two_different_public_addresses()
    {
        Assert.Equal(AddressComparison.Different, AddressComparison.Describe("203.0.113.10", "198.51.100.7"));
    }
}
