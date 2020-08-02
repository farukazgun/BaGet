using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BaGet.Core
{
    /// <summary>
    /// Validates that the new package doesn't already exist.
    /// </summary>
    public class UniquePackageMiddleware : IPackageIndexingMiddleware
    {
        private readonly IPackageService _packages;
        private readonly IPackageStorageService _storage;
        private readonly IOptionsSnapshot<BaGetOptions> _options;
        private readonly ILogger<UniquePackageMiddleware> _logger;

        public UniquePackageMiddleware(
            IPackageService packages,
            IPackageStorageService storage,
            IOptionsSnapshot<BaGetOptions> options,
            ILogger<UniquePackageMiddleware> logger)
        {
            _packages = packages ?? throw new ArgumentNullException(nameof(packages));
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task IndexAsync(PackageIndexingContext context, PackageIndexingDelegate next)
        {
            // Ensure this is a new package.
            if (await _packages.ExistsAsync(context.Package.Id, context.Package.Version, context.CancellationToken))
            {
                if (!_options.Value.AllowPackageOverwrites)
                {
                    _logger.LogWarning(
                        "Failed to index package {Id} {Version} as it already exists and overwrites are disabled.",
                        context.Package.Id,
                        context.Package.NormalizedVersionString);
                    context.Status = PackageIndexingStatus.PackageAlreadyExists;
                    return;
                }

                _logger.LogInformation(
                    "Package {Id} {Version} already exists. Deleting the package...",
                    context.Package.Id,
                    context.Package.NormalizedVersionString);

                await _packages.HardDeletePackageAsync(
                    context.Package.Id,
                    context.Package.Version,
                    context.CancellationToken);

                await _storage.DeleteAsync(
                    context.Package.Id,
                    context.Package.Version,
                    context.CancellationToken);
            }

            await next();
        }
    }
}
