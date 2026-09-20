<?php

declare(strict_types=1);

namespace Clog\Contract;

use Clog\Entity\Inventory\Contract\InventorySiteAvailabilityTrigger;
use Clog\Entity\Inventory\InventoryMutationContext;
use Clog\Entity\Site\Site;
use Clog\Entity\Site\SiteHydrator;
use DomainException;
use Eleph\Runtime\Identity\Identifier;
use Eleph\Runtime\Query\Queries;
use Eleph\Runtime\Storage\Criteria;
use Eleph\Runtime\Storage\EdgeFilter;

/**
 * PreCommit check that Inventory.site belongs to Inventory.location's allowed sites.
 */
final readonly class SiteAvailableAtLocation implements InventorySiteAvailabilityTrigger
{
    public function __construct(
        private Queries $queries,
        private SiteHydrator $hydrator,
    ) {
    }

    public function handle(InventoryMutationContext $context): void
    {
        $location = $context->pendingLocation();
        $sites = $context->pendingSite();

        if (null === $location || [] === $sites) {
            // Optional location or absent site leaves no pair to validate.
            return;
        }

        $criteria = Criteria::for('Site')->linkedTo(EdgeFilter::along('Location', 'sites', $location));

        /** @var list<Site> $allowed */
        $allowed = $this->queries->of($this->hydrator, $criteria)->all();

        foreach ($sites as $site) {
            if (!$this->isAllowed($site, $allowed)) {
                throw new DomainException(sprintf(
                    'Site %s is not one of this entry\'s location\'s available sites — check Location.sites.',
                    $site,
                ));
            }
        }
    }

    /**
     * @param list<Site> $allowed
     */
    private function isAllowed(Identifier $site, array $allowed): bool
    {
        foreach ($allowed as $candidate) {
            if ($candidate->getId()->equals($site)) {
                return true;
            }
        }

        return false;
    }
}
