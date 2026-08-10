using Xunit;

// Every test in this assembly migrates the whole appbridge_test database up and down against a
// real PostgreSQL instance (no Docker/Testcontainers in this environment — docs/SETUP-DEV.md).
// That's a shared, stateful resource: two tests wiping and rebuilding the schema concurrently race
// each other (a table dropped by one mid-query in another surfaces as "relation does not exist",
// not as a real isolation bug). Serializing the assembly is the correct fix, not a workaround.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
