// Tests share one app + database, so run them serially and reset the DB before each test.
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]
