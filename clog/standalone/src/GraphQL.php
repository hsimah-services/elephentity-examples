<?php

declare(strict_types=1);

namespace Clog\Standalone;

use Eleph\GraphQL\Manifest\Manifest;
use Eleph\GraphQL\Registration\MutationRegistrar;
use Eleph\GraphQL\Registration\TypeRegistrar;
use Eleph\GraphQL\Relay\GlobalId;
use Eleph\GraphQL\SchemaBuilder;
use Eleph\Runtime\Identity\EntityId;
use GraphQL\Error\UserError;
use GraphQL\Type\Schema;

/** Registers the compiled GraphQL manifest against the runtime; every type, query and mutation is generated. */
final class GraphQL
{
    public static function schema(Application $app): Schema
    {
        /** @var Manifest $manifest */
        $manifest = require dirname(__DIR__, 2) . '/generated/graphql/graphql-manifest.php';
        $builder = new SchemaBuilder();
        $registrar = new TypeRegistrar($manifest, $app->runtime);

        $builder->interface('Node', ['id' => ['type' => ['non_null' => 'ID']]], static function (object $node) use ($builder, $manifest) {
            foreach ($manifest->objects as $object) {
                $class = 'Clog\\Entity\\' . $object->entity . '\\' . $object->entity;

                if ($node instanceof $class) {
                    return $builder->type($object->name);
                }
            }

            return null;
        });
        $builder->object('PageInfo', ['fields' => [
            'hasNextPage' => ['type' => ['non_null' => 'Boolean']],
            'hasPreviousPage' => ['type' => ['non_null' => 'Boolean']],
            'startCursor' => ['type' => 'String'],
            'endCursor' => ['type' => 'String'],
        ]]);

        foreach ($registrar->enumConfigs() as $name => $config) {
            $builder->enum($name, $config);
        }

        foreach ($registrar->objectConfigs() as $name => $config) {
            $builder->object($name, $config);
        }

        foreach ($registrar->rootFieldConfigs() as $config) {
            $builder->field('RootQuery', $config['name'], $config['field']);
        }

        foreach ($registrar->connectionConfigs() as $config) {
            $builder->connection($config);
        }

        foreach ((new MutationRegistrar($manifest, $app->runtime))->configs() as $name => $config) {
            $builder->mutation($name, $config);
        }

        $builder->field('RootQuery', 'node', [
            'type' => 'Node',
            'args' => ['id' => ['type' => ['non_null' => 'ID']]],
            'resolve' => static function ($source, array $args) use ($app, $manifest): ?object {
                \assert(is_string($args['id']));
                $id = GlobalId::decode($args['id']);

                if (null === $id || !isset($manifest->objects[$id->type])) {
                    throw new UserError('Invalid node ID.');
                }

                return $app->runtime->find($manifest->objects[$id->type]->entity, EntityId::of($id->id));
            },
        ]);

        return $builder->build();
    }
}
