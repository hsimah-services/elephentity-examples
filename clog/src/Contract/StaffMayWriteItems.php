<?php

declare(strict_types=1);

namespace Clog\Contract;

use Clog\Entity\Item\Contract\ItemStaffWritePolicy;
use Clog\Entity\Item\Item;
use Clog\Entity\Item\ItemWriteContext;
use Eleph\Runtime\Policy\PolicyDecision;
use Eleph\Runtime\Policy\Viewer;

final readonly class StaffMayWriteItems implements ItemStaffWritePolicy
{
    public function decide(?Item $entity, ItemWriteContext $context, Viewer $viewer): PolicyDecision
    {
        if (!$viewer->can('edit_posts')) {
            return PolicyDecision::deny('Staff access is required to write items.');
        }

        // The action accessor returns null when discontinue is not running.
        $discontinuing = $context->discontinue();

        if (null !== $discontinuing && '' === trim($discontinuing->reason)) {
            return PolicyDecision::deny('Discontinuing an item requires a reason.');
        }

        return PolicyDecision::allow();
    }
}
