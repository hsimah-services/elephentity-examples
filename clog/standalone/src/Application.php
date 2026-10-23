<?php

declare(strict_types=1);

namespace Clog\Standalone;

use Clog\Runtime\RuntimeFactory;
use Eleph\Runtime\Gateway\Runtime;
use Eleph\SQLite\Database;
use Eleph\SQLite\Manifest\StorageManifest;
use Eleph\SQLite\Sql\FieldMap;
use Eleph\SQLite\Sql\QueryCompiler;
use Eleph\SQLite\SQLiteAdaptor;

/** One request's runtime: generated storage manifest, SQLite adaptor and the viewer's policies. */
final readonly class Application
{
    public Runtime $runtime;

    public function __construct(public Database $database, public Viewer $viewer)
    {
        Schema::requireReady($database);

        /** @var StorageManifest $manifest */
        $manifest = require dirname(__DIR__, 2) . '/generated/sqlite/storage-manifest.php';
        /** @var array<string, array<string, string>> $columns */
        $columns = $manifest->columns;
        $storage = new SQLiteAdaptor(
            $database,
            $manifest->tables,
            new FieldMap($columns),
            $manifest->placements,
            new QueryCompiler(placements: $manifest->placements),
        );

        $this->runtime = RuntimeFactory::create($storage, $viewer);
    }
}
