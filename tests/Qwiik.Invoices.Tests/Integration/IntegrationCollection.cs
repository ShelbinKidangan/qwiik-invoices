namespace Qwiik.Invoices.Tests.Integration;

/// <summary>
/// Shares one <see cref="SqlServerFixture"/> across all integration tests so the SQL
/// container starts once. Classes in this collection run serially against that shared DB
/// and reset it before each test.
/// </summary>
[CollectionDefinition("integration")]
public sealed class IntegrationCollection : ICollectionFixture<SqlServerFixture>;
