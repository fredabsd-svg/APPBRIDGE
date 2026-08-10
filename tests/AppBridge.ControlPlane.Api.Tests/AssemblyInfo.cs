using Xunit;

// AuthEndpointTests migrates the whole appbridge_test database up and down per test, against a real
// PostgreSQL instance (no Docker/Testcontainers — docs/SETUP-DEV.md). xUnit parallelizes test
// classes — and facts within a class, since each gets its own instance — by default; two of these
// racing would drop tables mid-query in one another (same failure mode fixed for the Infrastructure
// test assembly in T-203/T-206).
[assembly: CollectionBehavior(DisableTestParallelization = true)]
