<?php

declare(strict_types=1);

namespace Clog\Runtime;

use Clog\Contract\DefaultExpiryIsPaired;
use Clog\Contract\DiscontinueItem;
use Clog\Contract\ItemSearch;
use Clog\Contract\SignedInUsers;
use Clog\Contract\StaffMayWrite;
use Clog\Entity\Catalogue;
use Clog\Entity\Item\Contract\ItemDefaultExpiryUnitVerifier;
use Clog\Entity\Item\Contract\ItemDefaultExpiryValueVerifier;
use Clog\Entity\Item\Contract\ItemDiscontinueAction;
use Clog\Entity\Item\Contract\ItemSearchQuery;
use Clog\Entity\Item\ItemHydrator;
use Clog\Entity\Pattern\Record\Contract\RecordSignedInReadPolicy;
use Clog\Entity\Pattern\Record\Contract\RecordStaffWritePolicy;
use Eleph\Runtime\Catalogue\BootCheck;
use Eleph\Runtime\Gateway\Runtime;
use Eleph\Runtime\Gateway\UnitOfWorkFactory;
use Eleph\Runtime\Policy\ReadGate;
use Eleph\Runtime\Policy\ViewerProvider;
use Eleph\Runtime\Policy\WriteGate;
use Eleph\Runtime\Query\ValueDecoder;
use Eleph\Runtime\Storage\StorageAdaptor;
use Eleph\Runtime\Type\NullProcessorRegistry;

/**
 * Assembles storage, container, catalogue and runtime, binds every contract the spec
 * declares, then lets BootCheck prove none is missing.
 */
final class RuntimeFactory
{
    public static function create(StorageAdaptor $storage, ViewerProvider $viewers): Runtime
    {
        $container = new Container();
        $catalogue = new Catalogue($container);
        $runtime = new Runtime(
            $storage,
            $catalogue,
            new UnitOfWorkFactory($storage, $catalogue, new NullProcessorRegistry()),
            new ReadGate($catalogue, $viewers),
            new WriteGate($catalogue, $viewers),
        );

        $pairedExpiry = new DefaultExpiryIsPaired();

        $container->set(RecordSignedInReadPolicy::class, new SignedInUsers());
        $container->set(RecordStaffWritePolicy::class, new StaffMayWrite());
        $container->set(ItemDefaultExpiryUnitVerifier::class, $pairedExpiry);
        $container->set(ItemDefaultExpiryValueVerifier::class, $pairedExpiry);
        $container->set(ItemDiscontinueAction::class, new DiscontinueItem());
        $container->set(ItemSearchQuery::class, new ItemSearch($runtime->queries(), new ItemHydrator(new ValueDecoder())));

        (new BootCheck($catalogue, $container))->run();

        return $runtime;
    }
}
