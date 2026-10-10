<?php

declare(strict_types=1);
require __DIR__ . '/../vendor/autoload.php';

use Clog\Contract\SiteAvailableAtLocation;
use Clog\Entity\Inventory\{Inventory, InventoryPreCommitContext};
use Clog\Entity\Location\Location;
use Clog\Entity\Site\{Site, SiteHydrator};
use Eleph\Runtime\Capability\Capabilities;
use Eleph\Runtime\Identity\{EntityId, PendingId};
use Eleph\Runtime\Mutation\Mutation;
use Eleph\Runtime\Policy\{AnonymousViewerProvider, ReadGate};
use Eleph\Runtime\Query\{EdgeLoader, EntityQuery, Queries, ValueDecoder};
use Eleph\Runtime\Storage\{Criteria, Cursor, Page, Record, StorageAdaptor};
use Eleph\Runtime\Storage\Write\{WriteBatch, WriteResult};

final class Sites implements EntityQuery
{
    public function __construct(private array $sites)
    {
    }
    public function all(): array
    {
        return $this->sites;
    }
    public function first(): ?object
    {
        return $this->sites[0] ?? null;
    }
    public function count(): int
    {
        return count($this->sites);
    }
    public function exists(): bool
    {
        return [] !== $this->sites;
    }
    public function page(int $limit, ?Cursor $after = null): Page
    {
        return new Page(array_slice($this->sites, 0, $limit));
    }
}
final class OriginalEdges implements EdgeLoader
{
    public ?Location $location = null;
    public array $sites = [];
    public function toOne(string $entity, EntityId $id, string $edge): ?object
    {
        return $this->location;
    }
    public function toMany(string $entity, EntityId $id, string $edge): EntityQuery
    {
        return new Sites($this->sites);
    }
    public function inverseToOne(string $entity, string $edge, EntityId $id): ?object
    {
        throw new LogicException('Unused');
    }
    public function inverseToMany(string $entity, string $edge, EntityId $id): EntityQuery
    {
        throw new LogicException('Unused');
    }
}
final class AllowedSites implements StorageAdaptor
{
    public function capabilities(): Capabilities
    {
        return new Capabilities('test');
    }
    public function get(string $entity, EntityId $id): ?Record
    {
        throw new LogicException('Unused');
    }
    public function getMany(string $entity, array $ids): array
    {
        throw new LogicException('Unused');
    }
    public function count(Criteria $criteria): int
    {
        return count($this->query($criteria)->items);
    }
    public function query(Criteria $criteria): Page
    {
        if ('Site' !== $criteria->entity || 'Location' !== $criteria->links[0]->entity || 'sites' !== $criteria->links[0]->edge) {
            throw new LogicException('Wrong allowed-site query');
        }
        $location = $criteria->links[0]->from[0];
        $id = $location->equals(EntityId::of(10)) ? 1 : 2;
        return new Page([new Record('Site', EntityId::of($id), ['name' => 'Site ' . $id])]);
    }
    public function write(WriteBatch $batch): WriteResult
    {
        throw new LogicException('Unused');
    }
    public function transaction(callable $work): mixed
    {
        return $work();
    }
}
$edges = new OriginalEdges();
$now = new DateTimeImmutable('2026-10-10T00:00:00Z');
$edges->location = Location::of(EntityId::of(10), $edges, $now, $now, 'First');
$edges->sites = [Site::of(EntityId::of(1), 'First')];
$original = Inventory::of(EntityId::of(1), $edges, $now, $now, 'Entry', $now, null);
$queries = new Queries(new AllowedSites(), $edges, new ReadGate(new Clog\Entity\Catalogue(new Clog\Container()), new AnonymousViewerProvider()));
$rule = new SiteAvailableAtLocation($queries, new SiteHydrator(new ValueDecoder()));
$cases = [
    'site-only invalid replacement' => [false, false, fn (Mutation $m) => $m->edge('site')->set([EntityId::of(2)])],
    'site-only invalid addition' => [false, false, fn (Mutation $m) => $m->edge('site')->add(EntityId::of(2))],
    'location-only invalid replacement' => [false, false, fn (Mutation $m) => $m->edge('location')->set([EntityId::of(20)])],
    'location-only invalid addition' => [false, false, fn (Mutation $m) => $m->edge('location')->add(EntityId::of(20))],
    'unchanged valid pair' => [false, true, fn (Mutation $m) => $m->set('name', 'Rename')],
    'site-only valid replacement' => [false, true, fn (Mutation $m) => $m->edge('site')->set([EntityId::of(1)])],
    'site removal leaves no pair' => [false, true, fn (Mutation $m) => $m->edge('site')->remove(EntityId::of(1))],
    'site clear leaves no pair' => [false, true, fn (Mutation $m) => $m->edge('site')->set([])],
    'location clear leaves no pair' => [false, true, fn (Mutation $m) => $m->edge('location')->set([])],
    'replace both with valid pair' => [false, true, function (Mutation $m): void {
        $m->edge('location')->set([EntityId::of(20)]);
        $m->edge('site')->set([EntityId::of(2)]);
    }],
    'remove site then move location' => [false, true, function (Mutation $m): void {
        $m->edge('site')->remove(EntityId::of(1));
        $m->edge('location')->set([EntityId::of(20)]);
    }],
    'create valid pair' => [true, true, function (Mutation $m): void {
        $m->edge('location')->set([EntityId::of(10)]);
        $m->edge('site')->set([EntityId::of(1)]);
    }],
    'create invalid pair' => [true, false, function (Mutation $m): void {
        $m->edge('location')->set([EntityId::of(10)]);
        $m->edge('site')->set([EntityId::of(2)]);
    }],
];
foreach ($cases as $name => [$create, $expected, $change]) {
    $mutation = new Mutation('Inventory', $create ? new PendingId('Inventory') : $original->getId(), originalEntity: $create ? null : $original);
    $change($mutation);
    $accepted = true;
    try {
        $rule->handle(InventoryPreCommitContext::of($mutation));
    } catch (DomainException) {
        $accepted = false;
    }
    if ($accepted !== $expected) {
        throw new RuntimeException('Incorrect final-state validation: ' . $name);
    }
}
echo 'PASS: ' . count($cases) . " inventory final-state cases\n";
