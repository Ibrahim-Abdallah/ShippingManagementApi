using System.Reflection;

namespace ShippingManagementApi.UnitTests;

public sealed class ArchitectureTests
{
    [Fact]
    public void DomainAssembly_DoesNotReferenceOtherSolutionLayers()
    {
        var domainAssembly = Assembly.Load("ShippingManagementApi.Domain");
        var forbiddenReferences = domainAssembly.GetReferencedAssemblies()
            .Where(reference => reference.Name is not null &&
                reference.Name.StartsWith("ShippingManagementApi.", StringComparison.Ordinal) &&
                reference.Name != "ShippingManagementApi.Domain")
            .Select(reference => reference.Name)
            .ToArray();

        Assert.Empty(forbiddenReferences);
    }
}
