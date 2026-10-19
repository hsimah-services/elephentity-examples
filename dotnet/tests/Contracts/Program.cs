using Elephentity.Runtime.Catalogue;
using Elephentity.Runtime.Memory;
using Elephentity.Runtime.Storage;
using Elephentity.Runtime.Testing;
using Microsoft.Extensions.DependencyInjection;
using Shelf.App;

// The checker `eleph check` runs (eleph.json: "check": ["dotnet", "run", "--project", "tests/Contracts", "--", "eleph-check"]): every contract
// the generated catalogue lists must be provided by the application's container, built as the application builds it. Storage is the memory
// driver here, because the check resolves contracts and touches no data.
var services = new ServiceCollection();

services.AddShelf();
services.AddSingleton<IStorageAdaptor>(new MemoryAdaptor());

using var provider = services.BuildServiceProvider();

if (ContractCheck.TryRunConsole(args, provider.GetRequiredService<IEntityCatalogue>(), provider, Console.Out, out var exit))
{
    return exit;
}

Console.Error.WriteLine("Run with -- eleph-check.");

return 2;
