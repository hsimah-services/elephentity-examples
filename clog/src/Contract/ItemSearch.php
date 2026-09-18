<?php

declare(strict_types=1);

namespace Clog\Contract;

use Clog\Entity\Item\Contract\ItemSearchQuery;
use Clog\Entity\Item\Item;
use Clog\Entity\Item\ItemHydrator;
use Eleph\Runtime\Query\EntityQuery;
use Eleph\Runtime\Query\Queries;
use Eleph\Runtime\Storage\Criteria;
use Eleph\Runtime\Storage\Filter;
use Eleph\Runtime\Storage\Order;

/**
 * Application search query; the supplied hydrator preserves EntityQuery<Item> typing.
 */
final readonly class ItemSearch implements ItemSearchQuery
{
    public function __construct(
        private Queries $queries,
        private ItemHydrator $hydrator,
    ) {
    }

    /**
     * @return EntityQuery<Item>
     */
    public function find(string $term): EntityQuery
    {
        // Criteria filters are conjunctive; route numeric terms to barcode lookup.
        $criteria = Criteria::for('Item')
            ->where(1 === preg_match('/^\d+$/', $term)
                ? Filter::equals('barcode', $term)
                : Filter::contains('name', $term))
            ->orderBy(Order::ascending('name'));

        return $this->queries->of($this->hydrator, $criteria);
    }
}
