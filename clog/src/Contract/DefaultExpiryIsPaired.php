<?php

declare(strict_types=1);

namespace Clog\Contract;

use Clog\Entity\Enum\ExpiryUnit;
use Clog\Entity\Item\Contract\ItemDefaultExpiryUnitVerifier;
use Clog\Entity\Item\Contract\ItemDefaultExpiryValueVerifier;
use Clog\Entity\Item\ItemMutationContext;
use Eleph\Runtime\Verification\Verification;
use Eleph\Runtime\Verification\Violation;

/**
 * Both expiry fields must be set or both null. One implementation serves both field-verifier
 * contracts.
 */
final readonly class DefaultExpiryIsPaired implements ItemDefaultExpiryUnitVerifier, ItemDefaultExpiryValueVerifier
{
    public function verify(ExpiryUnit|int|null $value, ItemMutationContext $context): Verification
    {
        // pending() includes original values for untouched fields.
        $unit = $context->pendingDefaultExpiryUnit();
        $number = $context->pendingDefaultExpiryValue();

        if ((null === $unit) === (null === $number)) {
            return Verification::ok();
        }

        return Verification::failed(new Violation(
            'item.defaultExpiry.incomplete',
            'A default expiry needs both a unit and a value, or neither.',
        ));
    }
}
