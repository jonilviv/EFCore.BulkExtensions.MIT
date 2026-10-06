using EFCore.BulkOperations.Tests;
using Xunit.Sdk;
using Xunit.v3;

[assembly: Parallelization(Mode = ParallelMode.None)]

// xUnit v3 built-in assembly-wide fixture: created before any test runs and disposed after all tests finish.
[assembly: AssemblyFixture(typeof(DbAssemblyFixture))]