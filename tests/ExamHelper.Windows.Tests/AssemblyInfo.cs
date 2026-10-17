using Xunit;
// WPF XAML schema caches are process-wide; fixtures create isolated STA dispatchers.
// Run fixtures serially rather than initializing the same BAML types concurrently.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
