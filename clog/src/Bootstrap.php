<?php

declare(strict_types=1);

namespace Clog;

use Clog\Contract\DefaultExpiryIsPaired;
use Clog\Contract\DiscontinueItem;
use Clog\Contract\ItemSearch;
use Clog\Contract\SignedInUsers;
use Clog\Contract\SiteAvailableAtLocation;
use Clog\Contract\StaffMayWriteItems;
use Clog\Entity\Catalogue;
use Clog\Entity\Inventory\Contract\InventorySiteAvailabilityTrigger;
use Clog\Entity\Inventory\InventoryHydrator;
use Clog\Entity\Inventory\InventoryInput;
use Clog\Entity\Inventory\InventoryTriggers;
use Clog\Entity\Inventory\InventoryVerifiers;
use Clog\Entity\Item\Contract\ItemDefaultExpiryUnitVerifier;
use Clog\Entity\Item\Contract\ItemDefaultExpiryValueVerifier;
use Clog\Entity\Item\Contract\ItemDiscontinueAction;
use Clog\Entity\Item\Contract\ItemSearchQuery;
use Clog\Entity\Item\Contract\ItemStaffWritePolicy;
use Clog\Entity\Item\ItemFinder;
use Clog\Entity\Item\ItemHydrator;
use Clog\Entity\Item\ItemInput;
use Clog\Entity\Item\ItemTriggers;
use Clog\Entity\Item\ItemVerifiers;
use Clog\Entity\Location\LocationHydrator;
use Clog\Entity\Location\LocationInput;
use Clog\Entity\Location\LocationTriggers;
use Clog\Entity\Location\LocationVerifiers;
use Clog\Entity\Pattern\ClogPost\Contract\ClogPostSignedInReadPolicy;
use Clog\Entity\Site\SiteHydrator;
use Clog\Entity\Site\SiteInput;
use Clog\Entity\Site\SiteTriggers;
use Clog\Entity\Site\SiteVerifiers;
use Clog\Entity\User\UserHydrator;
use Clog\Entity\User\UserInput;
use Clog\Entity\User\UserTriggers;
use Clog\Entity\User\UserVerifiers;
use Eleph\Runtime\Catalogue\BootCheck;
use Eleph\Runtime\Gateway\Runtime;
use Eleph\Runtime\Gateway\UnitOfWorkFactory;
use Eleph\Runtime\Policy\ReadGate;
use Eleph\Runtime\Policy\WriteGate;
use Eleph\Runtime\Query\Queries;
use Eleph\Runtime\Query\ValueDecoder;
use Eleph\Runtime\Type\NullProcessorRegistry;
use Eleph\WordPress\Admin\Pages;
use Eleph\WordPress\Database\Database;
use Eleph\WordPress\Integrity\OrphanGuard;
use Eleph\WordPress\Manifest\StorageManifest;
use Eleph\WordPress\Migration\MigrationPlan;
use Eleph\WordPress\Migration\SchemaInstaller;
use Eleph\WordPress\Registration\PostTypeRegistrar;
use Eleph\WordPress\Viewer\WordPressViewerProvider;
use Eleph\WordPress\WordPress;
use Eleph\WPGraphQL\Plugin;

/**
 * Assembles storage, container, catalogue, and runtime before BootCheck. Lazy factories resolve
 * query-handler dependencies after the runtime exists.
 */
final class Bootstrap
{
    private const GENERATED = __DIR__ . '/../generated';

    private const STORAGE_MANIFEST = self::GENERATED . '/wordpress/storage-manifest.php';

    private const GRAPHQL_MANIFEST = self::GENERATED . '/wpgraphql/graphql-manifest.php';

    private const POST_TYPES = self::GENERATED . '/wordpress/post-types.php';

    private ?Runtime $runtime = null;

    public function __construct(private readonly Database $database)
    {
    }

    /**
     * The assembled framework, checked, built once.
     */
    public function runtime(): Runtime
    {
        if (null !== $this->runtime) {
            return $this->runtime;
        }

        $storage = WordPress::adaptor($this->database, $this->manifest());
        $container = new Container();
        $catalogue = new Catalogue($container);
        $viewers = new WordPressViewerProvider();

        $runtime = new Runtime(
            $storage,
            $catalogue,
            new UnitOfWorkFactory($storage, $catalogue, new NullProcessorRegistry()),
            new ReadGate($catalogue, $viewers),
            new WriteGate($catalogue, $viewers),
        );

        $this->register($container, $runtime);

        (new BootCheck($catalogue, $container))->run();

        return $this->runtime = $runtime;
    }

    /**
     * The GraphQL layer, for a project that speaks it.
     *
     * Hook `boot()` on `plugins_loaded`. Nothing else in the application depends on
     * this object existing — a project that speaks no GraphQL never loads it.
     */
    public function graphql(): Plugin
    {
        return Plugin::fromManifest(
            self::GRAPHQL_MANIFEST,
            $this->runtime(),
            new NullProcessorRegistry(),
        );
    }

    /**
     * The post types the spec compiled to. Hook `register()` on `init`.
     *
     * Compiled, not derived: this used to need the build-time `Schema`, so registering
     * post types meant shipping the spec compiler and parsing YAML on every request.
     */
    public function postTypes(): PostTypeRegistrar
    {
        return PostTypeRegistrar::fromManifest(self::POST_TYPES);
    }

    /** Hook register() on admin_menu. */
    public function adminPages(): Pages
    {
        return Pages::fromManifest(self::GENERATED . '/wordpress/admin-pages.php', $this->runtime());
    }

    /**
     * Hook `onPostDeleted()` on `before_delete_post`.
     *
     * Nothing in the framework sees someone empty the trash in wp-admin or another
     * plugin call `wp_delete_post()`. This clears the optional post link; the
     * entity survives and its relationships remain under Elephentity control.
     */
    public function orphanGuard(): OrphanGuard
    {
        return new OrphanGuard($this->manifest()->withPrefix($this->database->prefix()), $this->database);
    }

    /**
     * Applies additive migrations on activation. Any refusal prevents all planned changes.
     */
    public function install(): MigrationPlan
    {
        return (new SchemaInstaller($this->database, $this->manifest()))->install();
    }

    /**
     * Generated service factories plus application contract bindings.
     */
    private function register(Container $container, Runtime $runtime): void
    {
        $decoder = new ValueDecoder();

        $container
            // Resolve query dependencies after runtime assembly.
            ->set(ValueDecoder::class, static fn (): ValueDecoder => $decoder)
            ->set(Queries::class, static fn (): Queries => $runtime->queries())

            ->set(ItemHydrator::class, static fn (): object => new ItemHydrator($decoder))
            ->set(ItemInput::class, static fn (): object => new ItemInput($decoder))
            ->set(ItemTriggers::class, static fn (): object => new ItemTriggers())
            ->set(ItemVerifiers::class, static fn (Container $c): object => new ItemVerifiers(
                $c->get(ItemDefaultExpiryUnitVerifier::class),
                $c->get(ItemDefaultExpiryValueVerifier::class),
            ))
            ->set(ItemFinder::class, static fn (Container $c): object => new ItemFinder(
                $c->get(ItemSearchQuery::class),
            ))

            ->set(LocationHydrator::class, static fn (): object => new LocationHydrator($decoder))
            ->set(LocationInput::class, static fn (): object => new LocationInput($decoder))
            ->set(LocationTriggers::class, static fn (): object => new LocationTriggers())
            ->set(LocationVerifiers::class, static fn (): object => new LocationVerifiers())

            ->set(InventoryHydrator::class, static fn (): object => new InventoryHydrator($decoder))
            ->set(InventoryInput::class, static fn (): object => new InventoryInput($decoder))
            ->set(InventoryTriggers::class, static fn (Container $c): object => new InventoryTriggers(
                $c->get(InventorySiteAvailabilityTrigger::class),
            ))
            ->set(InventoryVerifiers::class, static fn (): object => new InventoryVerifiers())

            ->set(SiteHydrator::class, static fn (): object => new SiteHydrator($decoder))
            ->set(SiteInput::class, static fn (): object => new SiteInput($decoder))
            ->set(SiteTriggers::class, static fn (): object => new SiteTriggers())
            ->set(SiteVerifiers::class, static fn (): object => new SiteVerifiers())

            ->set(UserHydrator::class, static fn (): object => new UserHydrator($decoder))
            ->set(UserInput::class, static fn (): object => new UserInput($decoder))
            ->set(UserTriggers::class, static fn (): object => new UserTriggers())
            ->set(UserVerifiers::class, static fn (): object => new UserVerifiers())

            ->bind(ItemSearchQuery::class, static fn (Container $c): object => new ItemSearch(
                $c->get(Queries::class),
                $c->get(ItemHydrator::class),
            ))
            ->bind(ItemDefaultExpiryUnitVerifier::class, static fn (): object => new DefaultExpiryIsPaired())
            ->bind(ItemDefaultExpiryValueVerifier::class, static fn (Container $c): object => $c->get(
                ItemDefaultExpiryUnitVerifier::class,
            ))
            ->bind(ClogPostSignedInReadPolicy::class, static fn (): object => new SignedInUsers())
            ->bind(ItemStaffWritePolicy::class, static fn (): object => new StaffMayWriteItems())
            ->bind(ItemDiscontinueAction::class, static fn (): object => new DiscontinueItem())
            ->bind(InventorySiteAvailabilityTrigger::class, static fn (Container $c): object => new SiteAvailableAtLocation(
                $c->get(Queries::class),
                $c->get(SiteHydrator::class),
            ));
    }

    private function manifest(): StorageManifest
    {
        return WordPress::manifest(self::STORAGE_MANIFEST);
    }
}
