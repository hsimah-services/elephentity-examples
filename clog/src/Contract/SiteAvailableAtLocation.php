<?php

declare(strict_types=1);

namespace Clog\Contract;

use Clog\Entity\Inventory\Contract\InventorySiteAvailabilitySideEffect;
use Clog\Entity\Inventory\Inventory;
use Clog\Entity\Inventory\InventoryPreCommitContext;
use Clog\Entity\Site\Site;
use Clog\Entity\Site\SiteHydrator;
use DomainException;
use Eleph\Runtime\Identity\Identifier;
use Eleph\Runtime\Mutation\PendingEdge;
use Eleph\Runtime\Query\Queries;
use Eleph\Runtime\Storage\Criteria;
use Eleph\Runtime\Storage\EdgeFilter;
use RuntimeException;

/**
 * PreCommit check that Inventory.site belongs to Inventory.location's allowed sites.
 */
final readonly class SiteAvailableAtLocation implements InventorySiteAvailabilitySideEffect
{
    public function __construct(
        private Queries $queries,
        private SiteHydrator $hydrator,
    ) {
    }

    public function handle(InventoryPreCommitContext $context): void
    {
        $original = $context->originalEntity();
        if (!$context->isCreate() && !$original instanceof Inventory) {
            throw new RuntimeException('Inventory validation needs the original entity.');
        }
        $locationEdge = $context->location();
        $siteEdge = $context->site();
        if (!$locationEdge instanceof PendingEdge || !$siteEdge instanceof PendingEdge) {
            throw new RuntimeException('Inventory validation needs readable pending edge changes.');
        }
        $originalLocation = $original instanceof Inventory ? $original->getLocation()?->getId() : null;
        $locations = $this->finalIds($locationEdge, null === $originalLocation ? [] : [$originalLocation]);
        $location = [] === $locations ? null : $locations[array_key_last($locations)];
        $sites = $this->finalIds($siteEdge, $original instanceof Inventory
            ? array_map(static fn (Site $site): Identifier => $site->getId(), $original->site()->all())
            : []);

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
     * @param list<Identifier> $original
     *
     * @return list<Identifier>
     */
    private function finalIds(PendingEdge $edge, array $original): array
    {
        if ($edge->isReplacement()) {
            return $edge->added();
        }
        $ids = array_filter($original, static function (Identifier $id) use ($edge): bool {
            foreach ($edge->removed() as $removed) {
                if ($removed->equals($id)) {
                    return false;
                }
            }

            return true;
        });

        return [...$ids, ...$edge->added()];
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
