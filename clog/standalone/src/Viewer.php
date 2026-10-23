<?php

declare(strict_types=1);

namespace Clog\Standalone;

use Eleph\Runtime\Policy\Viewer as ViewerContract;
use Eleph\Runtime\Policy\ViewerProvider;

/** The signed-in account behind a request: anonymous, a reader, or an editor who may write. */
final readonly class Viewer implements ViewerContract, ViewerProvider
{
    public function __construct(private ?string $userId = null, private string $role = 'reader')
    {
    }

    public function id(): ?string
    {
        return $this->userId;
    }

    public function isAuthenticated(): bool
    {
        return null !== $this->userId;
    }

    public function hasRole(string $role): bool
    {
        return $this->isAuthenticated() && $this->role === $role;
    }

    public function can(string $capability): bool
    {
        return 'inventory.write' === $capability && $this->hasRole('editor');
    }

    public function viewer(): ViewerContract
    {
        return $this;
    }
}
