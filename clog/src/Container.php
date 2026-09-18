<?php

declare(strict_types=1);

namespace Clog;

use Closure;
use Psr\Container\ContainerInterface;
use RuntimeException;

/**
 * Minimal PSR-11 container with cached service factories.
 */
final class Container implements ContainerInterface
{
    /** @var array<string, Closure(self): object> */
    private array $factories = [];

    /** @var array<string, object> */
    private array $resolved = [];

    /**
     * @param Closure(self): object $factory
     */
    public function set(string $id, Closure $factory): self
    {
        $this->factories[$id] = $factory;

        return $this;
    }

    /**
     * Bind an interface to the class implementing it, so the contract and the
     * implementation are one entry rather than two.
     *
     * @param Closure(self): object $factory
     */
    public function bind(string $contract, Closure $factory): self
    {
        return $this->set($contract, $factory);
    }

    /**
     * Generic IDs preserve service types across PSR-11's mixed return boundary.
     *
     * @template T of object
     *
     * @param class-string<T> $id
     *
     * @return T
     */
    public function get(string $id): object
    {
        $service = $this->resolved[$id] ??= ($this->factories[$id] ?? throw new RuntimeException(
            sprintf('Nothing is bound to %s.', $id),
        ))($this);

        if (!$service instanceof $id) {
            throw new RuntimeException(sprintf('%s is bound to a %s.', $id, $service::class));
        }

        return $service;
    }

    public function has(string $id): bool
    {
        return isset($this->factories[$id]);
    }
}
