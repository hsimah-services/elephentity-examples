<?php

declare(strict_types=1);
require __DIR__ . '/../vendor/autoload.php';

use Clog\Contract\DefaultExpiryIsPaired;
use Clog\Entity\Enum\ExpiryUnit;
use Clog\Entity\Item\ItemVerifiers;
use Eleph\Runtime\Identity\{EntityId, PendingId};
use Eleph\Runtime\Mutation\Mutation;
use Eleph\Runtime\Type\NullProcessorRegistry;
use Eleph\Runtime\UnitOfWork\VerificationPipeline;

$rule = new DefaultExpiryIsPaired();
$pipeline = new VerificationPipeline(['Item' => new ItemVerifiers($rule, $rule)], [], new NullProcessorRegistry());
$paired = ['defaultExpiryUnit' => ExpiryUnit::Days, 'defaultExpiryValue' => 7];
$empty = ['defaultExpiryUnit' => null, 'defaultExpiryValue' => null];
$cases = [
    'clear unit alone' => [$paired, ['defaultExpiryUnit' => null], false, false],
    'clear number alone' => [$paired, ['defaultExpiryValue' => null], false, false],
    'clear both' => [$paired, $empty, false, true],
    'unchanged pair' => [$paired, ['name' => 'Rename'], false, true],
    'set unit alone' => [$empty, ['defaultExpiryUnit' => ExpiryUnit::Months], false, false],
    'set number alone' => [$empty, ['defaultExpiryValue' => 7], false, false],
    'set both' => [$empty, $paired, false, true],
    'create without expiry' => [[], [], true, true],
    'create paired expiry' => [[], $paired, true, true],
    'create incomplete expiry' => [[], ['defaultExpiryUnit' => ExpiryUnit::Days], true, false],
];
foreach ($cases as $name => [$original, $changes, $create, $expected]) {
    $mutation = new Mutation('Item', $create ? new PendingId('Item') : EntityId::of(1), $original);
    foreach ($changes as $field => $value) {
        $mutation->set($field, $value);
    }
    $violations = $pipeline->verify($mutation);
    if (([] === $violations) !== $expected) {
        throw new RuntimeException('Incorrect nullable final-state verification: ' . $name);
    }
    if (!$expected && 2 !== count($violations)) {
        throw new RuntimeException('Both expiry verifiers must inspect the final pair: ' . $name);
    }
}
echo 'PASS: ' . count($cases) . " nullable expiry final-state cases\n";
