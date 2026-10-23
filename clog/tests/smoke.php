<?php

declare(strict_types=1);

require dirname(__DIR__) . '/standalone/bootstrap.php';

use Clog\Entity\Enum\ExpiryUnit;
use Clog\Standalone\Application;
use Clog\Standalone\GraphQL;
use Clog\Standalone\Schema;
use Clog\Standalone\Viewer;
use Eleph\SQLite\Database;
use GraphQL\GraphQL as Executor;

/** A basic end-to-end check that the generated code, storage, contracts and schema still fit together. */
function expect(bool $condition, string $message): void
{
    if (!$condition) {
        throw new RuntimeException($message);
    }
}

function rejects(callable $work, string $message): void
{
    try {
        $work();
    } catch (Throwable) {
        return;
    }

    throw new RuntimeException($message);
}

$db = new Database(':memory:');
Schema::install($db);
$app = new Application($db, new Viewer('1', 'editor'));
$runtime = $app->runtime;

$site = $runtime->create('Site', ['name' => 'Loft']);
$location = $runtime->create('Location', ['name' => 'Pantry']);
$item = $runtime->create('Item', ['name' => 'Torch', 'barcode' => '00123']);
$runtime->create('Inventory', [
    'name' => 'Torch',
    'dateAdded' => '2026-10-01T00:00:00Z',
    'item' => (string) $item->id,
    'location' => (string) $location->id,
    'site' => [(string) $site->id],
]);

rejects(fn () => $runtime->create('Item', ['name' => 'Copy', 'barcode' => '00123']), 'Barcodes are unique');
rejects(fn () => $runtime->create('Item', ['name' => 'Half', 'defaultExpiryUnit' => ExpiryUnit::Days]), 'Expiry needs both halves');
rejects(fn () => $runtime->runAction('Item', 'discontinue', $item->id, ['reason' => ' ']), 'Discontinuing needs a reason');
rejects(fn () => (new Application($db, new Viewer('2', 'reader')))->runtime->create('Item', ['name' => 'Denied']), 'Readers cannot write');

$schema = GraphQL::schema($app);
$schema->assertValid();
$result = Executor::executeQuery($schema, '{ clogItems(first: 5) { totalCount } }')->toArray();
expect(1 === ($result['data']['clogItems']['totalCount'] ?? null), 'GraphQL lists the item');

echo "PASS: smoke test\n";
