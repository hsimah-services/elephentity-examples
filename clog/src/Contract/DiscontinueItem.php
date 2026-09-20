<?php

declare(strict_types=1);

namespace Clog\Contract;

use Clog\Entity\Item\Contract\ItemDiscontinueAction;
use Clog\Entity\Item\ItemDiscontinueContext;

/**
 * Clears the barcode. The reason is consumed by the write policy, not persisted.
 */
final readonly class DiscontinueItem implements ItemDiscontinueAction
{
    public function handle(ItemDiscontinueContext $context, string $reason): void
    {
        $context->setBarcode(null);
    }
}
