<?php

declare(strict_types=1);

namespace Clog\Contract;

use Clog\Entity\Item\Contract\ItemDiscontinueAction;
use Clog\Entity\Item\ItemDiscontinueContext;
use DomainException;

/**
 * Clears the barcode. The reason is required but not persisted.
 */
final readonly class DiscontinueItem implements ItemDiscontinueAction
{
    public function handle(ItemDiscontinueContext $context, string $reason): void
    {
        if ('' === trim($reason)) {
            throw new DomainException('Discontinuing an item requires a reason.');
        }

        $context->setBarcode(null);
    }
}
