<?php

declare(strict_types=1);

namespace Clog\Contract;

use Clog\Entity\Pattern\Record\Contract\RecordSignedInReadPolicy;
use Clog\Entity\Pattern\Record\Record;
use Eleph\Runtime\Policy\PolicyDecision;
use Eleph\Runtime\Policy\Viewer;

final readonly class SignedInUsers implements RecordSignedInReadPolicy
{
    public function decide(Record $entity, Viewer $viewer): PolicyDecision
    {
        return $viewer->isAuthenticated()
            ? PolicyDecision::allow()
            : PolicyDecision::deny('Sign in to read this entity.');
    }
}
