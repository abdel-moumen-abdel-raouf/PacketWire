using System.Reflection;
using PacketWire;

namespace PacketWire.Abstractions.Tests;

/// <summary>
/// Verifies reflection metadata and usage targets of PacketWire attributes.
/// </summary>
public sealed class AttributeUsageTests
{
    /// <summary>
    /// Verifies that <see cref="PacketAttribute"/> targets classes and structs only and is not inherited.
    /// </summary>
    [Fact]
    public void PacketAttributeTargetsClassesAndStructsOnly()
    {
        AttributeUsageAttribute usage = GetUsage<PacketAttribute>();

        Assert.Equal(
            AttributeTargets.Class | AttributeTargets.Struct,
            usage.ValidOn);

        Assert.False(usage.AllowMultiple);
        Assert.False(usage.Inherited);
    }

    /// <summary>
    /// Verifies that <see cref="PacketContractAttribute"/> targets classes and structs only and is not inherited.
    /// </summary>
    [Fact]
    public void PacketContractAttributeTargetsClassesAndStructsOnly()
    {
        AttributeUsageAttribute usage = GetUsage<PacketContractAttribute>();

        Assert.Equal(
            AttributeTargets.Class | AttributeTargets.Struct,
            usage.ValidOn);

        Assert.False(usage.AllowMultiple);
        Assert.False(usage.Inherited);
    }

    /// <summary>
    /// Verifies that <see cref="PacketProtocolAttribute"/> targets classes and structs only and is not inherited.
    /// </summary>
    [Fact]
    public void PacketProtocolAttributeTargetsClassesAndStructsOnly()
    {
        AttributeUsageAttribute usage = GetUsage<PacketProtocolAttribute>();

        Assert.Equal(
            AttributeTargets.Class | AttributeTargets.Struct,
            usage.ValidOn);

        Assert.False(usage.AllowMultiple);
        Assert.False(usage.Inherited);
    }

    /// <summary>
    /// Verifies that field metadata attributes target properties only and support inheritance.
    /// </summary>
    /// <param name="attributeType">The attribute type under test.</param>
    [Theory]
    [InlineData(typeof(PacketFieldAttribute))]
    [InlineData(typeof(PacketIgnoreAttribute))]
    [InlineData(typeof(FixedStringAttribute))]
    [InlineData(typeof(OptionalAttribute))]
    [InlineData(typeof(MaxCountAttribute))]
    public void FieldMetadataAttributesTargetPropertiesOnly(
        Type attributeType)
    {
        AttributeUsageAttribute usage =
            GetUsage(attributeType);

        Assert.Equal(
            AttributeTargets.Property,
            usage.ValidOn);

        Assert.False(usage.AllowMultiple);
        Assert.True(usage.Inherited);
    }

    /// <summary>
    /// Retrieves the <see cref="AttributeUsageAttribute"/> for the specified generic attribute type.
    /// </summary>
    /// <typeparam name="TAttribute">The attribute type.</typeparam>
    /// <returns>The declared attribute usage specification.</returns>
    private static AttributeUsageAttribute GetUsage<TAttribute>()
        where TAttribute : Attribute
    {
        return GetUsage(typeof(TAttribute));
    }

    /// <summary>
    /// Retrieves the <see cref="AttributeUsageAttribute"/> declared on the specified attribute type.
    /// </summary>
    /// <param name="attributeType">The attribute type.</param>
    /// <returns>The declared attribute usage specification.</returns>
    private static AttributeUsageAttribute GetUsage(
        Type attributeType)
    {
        return attributeType
            .GetCustomAttribute<AttributeUsageAttribute>()
            ?? throw new InvalidOperationException(
                $"AttributeUsageAttribute was not found on {attributeType.FullName}.");
    }
}
